using System.Text.Json;
using ExcelDataReader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using CureFlow.Infrastructure.Identity;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
using CureFlow.Application.Validation;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Entities.Lifestyle;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.External;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services;

public class PatientService : IPatientService
{
    private readonly ICureFlowDbSession _db;
    private readonly IAuditService _audit;
    private readonly INotificationPublisher _notifications;
    private readonly ITenantContext _tenant;
    private readonly ILogger<PatientService> _logger;
    private readonly IIdempotentCommandExecutor _commands;
    private readonly IPatientIdentifierService _identifiers;
    private readonly IUserPermissionService _permissions;
    private readonly IConfiguration _config;

    public PatientService(
        ICureFlowDbSession db,
        IAuditService audit,
        INotificationPublisher notifications,
        ITenantContext tenant,
        ILogger<PatientService> logger, IIdempotentCommandExecutor commands, IPatientIdentifierService identifiers, IUserPermissionService permissions, IConfiguration config)
    {
        _commands=commands; _identifiers=identifiers; _permissions=permissions; _config=config;
        _db = db;
        _audit = audit;
        _logger = logger;
        _notifications = notifications;
        _tenant = tenant;
    }

    public Task<IdempotentCommandResult> RegisterAsync(CreatePatientRequest req,string key,CancellationToken ct=default)
    {
        if(req.FacilityId is null||req.FacilityId==Guid.Empty)throw new ValidationException("Supply an explicit registration facility.");
        var horizon=_config.GetValue<int?>("PatientRegistration:ApprovedRetryHorizonHours");
        if(horizon is not >0 or >8760)throw new DomainException("Registration retry policy is not configured.",503);
        return _commands.ExecuteAsync("patient.register",key,req,TimeSpan.FromHours(horizon.Value),async(store,token)=>{
            if(!_tenant.IsAuthenticated||_tenant.IsPlatformUser||!_tenant.UserId.HasValue)throw new ForbiddenException();
            var actor=await store.QueryFirstOrDefaultAsync<User>("""
                SELECT * FROM "Users" WHERE "Id"=@id AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                """,new{id=_tenant.UserId.Value},ct:token);
            if(actor is null||!(await _permissions.GetPermissionCodesAsync(actor,token,store)).Contains(CureFlowPermissions.PatientCreate,StringComparer.Ordinal))throw new ForbiddenException();
            var facility=await store.QueryFirstOrDefaultAsync<Guid?>("""
                SELECT public.cureflow_lock_active_facility(@TenantId,@facilityId)
                """,new{facilityId=req.FacilityId.Value},ct:token);
            if(!facility.HasValue)throw new NotFoundException("Active registration facility");
        },async(store,token)=>{
            // All existing local notification/audit writes use the same scoped session and outer transaction.
            var id=await CreateAsync(req,token);
            await _identifiers.AllocateLocalMrnAsync(store,req.FacilityId.Value,id,token);
            return(new CommandReceipt(id,null,CommandOutcome.Created),201);
        },ct);
    }
    public async Task<Guid> CreateAsync(CreatePatientRequest req, CancellationToken ct = default)
    {
        PatientDemographicRules.ValidateSource(req.DemographicSource);
        PatientDemographicRules.ValidateBirth(req.BirthDate);
        var p = new Patient
        {
            Name = req.Name.Trim(),
            Phone = WhatsappPhoneHelper.Normalize(req.Phone),
            Email = req.Email,
            Age = req.Age,
            Gender = req.Gender ?? Gender.Unknown,
            BloodGroup = req.BloodGroup ?? BloodGroup.Unknown,
            Department = req.Department,
            InquirySource = req.InquirySource ?? "manual",
            ReferringDoctorId = req.ReferringDoctorId,
            AddressLine1 = req.AddressLine1,
            City = req.City,
            State = req.State,
            Pincode = req.Pincode,
            Tags = req.Tags ?? new List<string>(),
            Notes = req.Notes,
            Occupation = req.Occupation,
        };
        p.BirthDatePrecision="unknown";
        PatientDemographicRules.Apply(p,req.BirthDate);
        await _db.TransactionAsync(async db =>
        {
            if (req.ReferringDoctorId.HasValue)
            {
                if (req.ReferringDoctorId == Guid.Empty)
                    throw new ValidationException("Select a valid referring doctor.");
                _ = await db.QueryFirstOrDefaultAsync<ReferringDoctor>("""
                    SELECT "Id" FROM "ReferringDoctors" WHERE "Id"=@id AND "TenantId"=@TenantId
                        AND NOT "IsDeleted" AND "IsActive" FOR SHARE
                    """, new { id = req.ReferringDoctorId.Value }, ct: ct)
                    ?? throw new NotFoundException("Referring doctor");
            }
            await RequirePatientWriteAsync(db,CureFlowPermissions.PatientCreate,ct);
            await SetDemographicSourceAsync(db,req.DemographicSource,ct);
            await db.InsertAsync(p, ct: ct);
        }, ct);
        await _audit.LogAsync("patient.create", "patient", p.Id.ToString(), null, ct);

        await _notifications.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.PatientCreated,
            "New patient registered",
            $"{p.Name.Trim()} was added to the patient registry",
            NotificationSeverity.Info,
            EntityType: "patient",
            EntityId: p.Id,
            ActionUrl: $"/patients/{p.Id}",
            CreatorUserId: _tenant.UserId, DedupeKey: $"patient-created:{p.Id:N}"), ct);

        return p.Id;
    }

    public async Task<PatientDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.GetByIdAsync<Patient>(id, ct: ct)
            ?? throw new NotFoundException("Patient");
        return MapDetail(p);
    }

    public async Task<PatientHolisticViewDto> GetHolisticViewAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.GetByIdAsync<Patient>(id, ct: ct)
            ?? throw new NotFoundException("Patient");

        var lifestyle = await _db.QueryFirstOrDefaultAsync<LifestyleProfile>(
            """SELECT * FROM "LifestyleProfiles" WHERE "PatientId" = @patientId LIMIT 1""",
            new { patientId = id }, ct: ct);
        var allergies = await _db.QueryAsync<Allergy>(
            """SELECT * FROM "Allergies" WHERE "PatientId" = @patientId ORDER BY "CreatedAt" DESC""",
            new { patientId = id }, ct: ct);
        var rxList = await PrescriptionGraphLoader.LoadAsync(
            _db, @"""PatientId"" = @patientId", new { patientId = id },
            """ORDER BY "PrescribedAt" DESC LIMIT 20""", ct);
        var vitals = await _db.QueryAsync<VitalSigns>(
            """SELECT * FROM "VitalSigns" WHERE "PatientId" = @patientId ORDER BY "MeasuredAt" DESC LIMIT 20""",
            new { patientId = id }, ct: ct);
        var labs = await _db.QueryAsync<LabReport>(
            """SELECT * FROM "LabReports" WHERE "PatientId" = @patientId ORDER BY "ReportedAt" DESC LIMIT 20""",
            new { patientId = id }, ct: ct);
        var notes = await _db.QueryAsync<ClinicalNote>(
            """SELECT * FROM "ClinicalNotes" WHERE "PatientId" = @patientId AND "TenantId" = @TenantId AND "IsDeleted" = false AND ("FinalizedAt" IS NOT NULL OR "AuthorUserId" = @userId) ORDER BY "CreatedAt" DESC LIMIT 20""",
            new { patientId = id, userId = _tenant.UserId }, ct: ct);
        var med = await _db.QueryAsync<MedicalHistoryItem>(
            """SELECT * FROM "MedicalHistory" WHERE "PatientId" = @patientId""",
            new { patientId = id }, ct: ct);
        var fam = await _db.QueryAsync<FamilyHistoryItem>(
            """SELECT * FROM "FamilyHistory" WHERE "PatientId" = @patientId""",
            new { patientId = id }, ct: ct);
        var apptWhere = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var appts = await _db.QueryAsync<Appointment>(
            $"""
            SELECT * FROM "Appointments"
            WHERE {apptWhere} AND "PatientId" = @patientId
            ORDER BY "ScheduledAt" DESC LIMIT 20
            """,
            new { patientId = id }, ct: ct);

        return new PatientHolisticViewDto(
            MapDetail(p),
            lifestyle == null ? null : MapLifestyle(lifestyle),
            allergies.Select(a => new AllergyDto(a.Id, a.PatientId, a.Type, a.Allergen, a.Severity,
                a.Reaction, a.FirstObserved, a.Notes, a.RecordedByName, a.CreatedAt)).ToList(),
            rxList.Select(MapPrescription).ToList(),
            vitals.Cast<object>().ToList(),
            labs.Cast<object>().ToList(),
            notes.Cast<object>().ToList(),
            med.Cast<object>().ToList(),
            fam.Cast<object>().ToList(),
            appts.Cast<object>().ToList());
    }

    public async Task<PagedResult<PatientSummaryDto>> ListAsync(
        string? q, string? status, string? department, string? tag, string? inquirySource,
        int page, int pageSize, CancellationToken ct = default,
        string sortBy = "name", string sortDirection = "asc", bool includeAppointments = false)
    {
        PatientSearchRules.Validate(q, status, department, tag, inquirySource);
        if (page > int.MaxValue / Pagination.MaxPageSize)
            throw new ValidationException("Patient page is outside the supported range. Use cursor search for long listings.");
        var direction = sortDirection.ToLowerInvariant() switch
        {
            "asc" => "ASC",
            "desc" => "DESC",
            _ => throw new ValidationException("Sort direction must be asc or desc.")
        };
        // Only trusted SQL expressions may reach ORDER BY.
        var sortExpression = sortBy.ToLowerInvariant() switch
        {
            "name" => """NULLIF(LOWER(BTRIM("Name")), '')""",
            "phone" => """NULLIF(BTRIM("Phone"), '')""",
            "email" => """NULLIF(LOWER(BTRIM("Email")), '')""",
            "age" => "\"Age\"",
            "gender" => "\"Gender\"",
            "department" => """NULLIF(LOWER(BTRIM("Department")), '')""",
            "status" => """CASE "Status" WHEN 0 THEN 'new inquiry' WHEN 1 THEN 'contacted' WHEN 2 THEN 'appointment scheduled' WHEN 3 THEN 'follow up pending' WHEN 4 THEN 'visited' WHEN 5 THEN 'no response' WHEN 6 THEN 'lost' WHEN 7 THEN 're engagement' END""",
            "inquiry_source" => """NULLIF(LOWER(BTRIM("InquirySource")), '')""",
            "last_contact_at" => "\"LastContactAt\"",
            "created_at" => "\"CreatedAt\"",
            "next_appointment" when includeAppointments => """(SELECT MIN(a."ScheduledAt") FROM "Appointments" a WHERE a."PatientId" = "Patients"."Id" AND a."TenantId" = "Patients"."TenantId" AND a."IsDeleted" = false AND a."Status" IN (0, 1) AND a."ScheduledAt" >= @now)""",
            _ => throw new ValidationException("Unsupported patient sort column.")
        };
        var (normalizedPage, normalizedPageSize, skip) = Pagination.Normalize(page, pageSize);
        var filters = new List<string> { SqlFragments.WhereActive<Patient>(ignoreTenant: false) };
        var param = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            filters.Add("(\"Name\" ILIKE @q OR \"Phone\" ILIKE @q OR (\"Email\" IS NOT NULL AND \"Email\" ILIKE @q))");
            param["q"] = PatientSearchRules.Pattern(q);
        }
        if (!string.IsNullOrEmpty(status) && EnumParseHelper.TryParseSnakeCase<LeadStatus>(status, out var st))
        {
            filters.Add("\"Status\" = @status");
            param["status"] = (int)st;
        }
        if (!string.IsNullOrEmpty(department))
        {
            filters.Add("\"Department\" = @department");
            param["department"] = department;
        }
        if (!string.IsNullOrEmpty(tag))
        {
            filters.Add("\"Tags\" @> @tagFilter::jsonb");
            param["tagFilter"] = JsonSerializer.Serialize(new[] { tag });
        }
        if (!string.IsNullOrEmpty(inquirySource))
        {
            filters.Add("\"InquirySource\" = @inquirySource");
            param["inquirySource"] = inquirySource;
        }

        var where = " WHERE " + string.Join(" AND ", filters);
        var total = await _db.QuerySingleAsync<int>(
            $"""SELECT COUNT(*) FROM "Patients"{where}""", param, ct: ct);

        param["skip"] = skip;
        param["take"] = normalizedPageSize;
        param["now"] = DateTime.UtcNow;
        var rows = await _db.QueryAsync<Patient>(
            $"""
            SELECT * FROM "Patients"{where}
            ORDER BY {sortExpression} {direction} NULLS LAST, LOWER("Name") ASC, "Id" ASC
            OFFSET @skip LIMIT @take
            """, param, ct: ct);

        var nextAppointments = new Dictionary<Guid, DateTime?>();
        if (includeAppointments && rows.Count > 0)
        {
            var appointmentWhere = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
            var upcoming = await _db.QueryAsync<Appointment>(
                $"""SELECT "PatientId", MIN("ScheduledAt") AS "ScheduledAt" FROM "Appointments" WHERE {appointmentWhere} AND "PatientId" = ANY(@ids) AND "Status" IN (0, 1) AND "ScheduledAt" >= @now GROUP BY "PatientId" """,
                new { ids = rows.Select(p => p.Id).ToArray(), now = param["now"] }, ct: ct);
            foreach (var appointment in upcoming)
                nextAppointments[appointment.PatientId] = appointment.ScheduledAt;
        }

        var items = rows.Select(p => new PatientSummaryDto(p.Id, p.Name, p.Phone, p.Email, p.Age, p.Gender,
            p.Department, p.Status, p.Tags, p.InquirySource, p.CreatedAt, p.LastContactAt, nextAppointments.GetValueOrDefault(p.Id))).ToList();

        return new PagedResult<PatientSummaryDto>
        {
            Items = items,
            Total = total,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
        };
    }

    public async Task<PatientSearchPage> SearchAsync(string? q, string? status, string? department, string? tag,
        string? inquirySource, Guid? afterId, int pageSize, CancellationToken ct = default)
    {
        PatientSearchRules.Validate(q, status, department, tag, inquirySource);
        if (pageSize is < 1 or > 100 || afterId == Guid.Empty)
            throw new ValidationException("Select a page size from 1 to 100 and a valid patient cursor.");
        int? state = !string.IsNullOrWhiteSpace(status) && EnumParseHelper.TryParseSnakeCase<LeadStatus>(status, out var parsed)
            ? (int)parsed : null;
        PatientSearchPage? result = null;
        await _db.TransactionAsync(async db =>
        {
            // The cursor contains an already returned local ID, never demographics or arbitrary SQL.
            // CreatedAt is immutable for normal patient edits; Id breaks every timestamp/name tie.
            var boundary = afterId.HasValue ? await db.QueryFirstOrDefaultAsync<Patient>("""
                SELECT "Id", "CreatedAt" FROM "Patients"
                WHERE "Id"=@afterId AND "TenantId"=@TenantId AND NOT "IsDeleted"
                """, new { afterId }, ct: ct) : null;
            if (afterId.HasValue && boundary is null)
                throw new ValidationException("The patient cursor is unavailable. Restart this search.");
            var rows = await db.QueryAsync<PatientSearchRow>("""
                SELECT "Id", "Name", "Phone", "Age", "Gender", "Department"
                FROM "Patients"
                WHERE "TenantId"=@TenantId AND NOT "IsDeleted"
                    AND (CAST(@q AS text) IS NULL OR "Name" ILIKE @q OR "Phone" ILIKE @q OR "Email" ILIKE @q)
                    AND (CAST(@state AS integer) IS NULL OR "Status"=@state)
                    AND (CAST(@department AS text) IS NULL OR "Department"=@department)
                    AND (CAST(@tag AS text) IS NULL OR "Tags" @> CAST(@tag AS jsonb))
                    AND (CAST(@source AS text) IS NULL OR "InquirySource"=@source)
                    AND (CAST(@afterId AS uuid) IS NULL OR ("CreatedAt", "Id") > (CAST(@afterCreated AS timestamptz), CAST(@afterId AS uuid)))
                ORDER BY "CreatedAt" ASC, "Id" ASC LIMIT @take
                """, new
                {
                    q = PatientSearchRules.Pattern(q), state,
                    department = string.IsNullOrWhiteSpace(department) ? null : department,
                    tag = string.IsNullOrWhiteSpace(tag) ? null : JsonSerializer.Serialize(new[] { tag }),
                    source = string.IsNullOrWhiteSpace(inquirySource) ? null : inquirySource,
                    afterId, afterCreated = boundary?.CreatedAt, take = pageSize + 1
                }, ct: ct);
            var items = rows.Take(pageSize).Select(p => new PatientSearchItem(p.Id, p.Name, p.Phone, p.Age, p.Gender, p.Department)).ToList();
            result = new(items, rows.Count > pageSize ? items[^1].Id : null);
        }, ct);
        return result!;
    }

    private sealed class PatientSearchRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public int? Age { get; set; }
        public Gender Gender { get; set; }
        public string? Department { get; set; }
    }

    public async Task UpdateAsync(Guid id, UpdatePatientRequest req, CancellationToken ct = default)
    {
        PatientDemographicRules.ValidateSource(req.DemographicSource);
        PatientDemographicRules.ValidateBirth(req.BirthDate);
        if(req.DateOfBirth.HasValue&&req.BirthDate is not null)throw new ValidationException("Use either legacy date_of_birth or explicit calendar evidence.");
        if(req.ExpectedETag is null)throw new DomainException("Supply the current patient ETag.",428);
        var clear = req.ClearFields ?? [];
        var clearable = new Dictionary<string,string>(StringComparer.Ordinal)
        {
            ["email"]=nameof(Patient.Email),["age"]=nameof(Patient.Age),["date_of_birth"]=nameof(Patient.DateOfBirth),
            ["department"]=nameof(Patient.Department),["assigned_staff_id"]=nameof(Patient.AssignedStaffId),
            ["notes"]=nameof(Patient.Notes),["follow_up_date"]=nameof(Patient.FollowUpDate),
            ["occupation"]=nameof(Patient.Occupation),["marital_status"]=nameof(Patient.MaritalStatus),
            ["gov_id_type"]=nameof(Patient.GovIdType),["gov_id_number"]=nameof(Patient.GovIdNumber),
            ["address_line1"]=nameof(Patient.AddressLine1),["address_line2"]=nameof(Patient.AddressLine2),
            ["city"]=nameof(Patient.City),["state"]=nameof(Patient.State),["pincode"]=nameof(Patient.Pincode),
            ["emergency_contact_name"]=nameof(Patient.EmergencyContactName),["emergency_contact_phone"]=nameof(Patient.EmergencyContactPhone),
            ["emergency_contact_relation"]=nameof(Patient.EmergencyContactRelation)
        };
        if(clear.Count>clearable.Count||clear.Distinct(StringComparer.Ordinal).Count()!=clear.Count||clear.Any(x=>x is null||!clearable.ContainsKey(x)))
            throw new ValidationException("Supply distinct supported optional fields in clear_fields.");
        foreach(var field in clear)
            if(typeof(UpdatePatientRequest).GetProperty(clearable[field])!.GetValue(req) is not null)
                throw new ValidationException("A field cannot be supplied and explicitly cleared in the same request.");
        await _db.TransactionAsync(async db =>
        {
        await RequirePatientWriteAsync(db,CureFlowPermissions.PatientEdit,ct);
        var p = await db.QueryFirstOrDefaultAsync<Patient>("""
            SELECT * FROM "Patients" WHERE "Id"=@id AND "TenantId"=@TenantId AND NOT "IsDeleted" FOR UPDATE
            """,new{id},ct:ct)
            ?? throw new NotFoundException("Patient");
        if(PatientConcurrency.Tag(p)!=req.ExpectedETag)throw new DomainException("Patient details changed. Reload before saving your edits.",412);
        if(req.BirthDate is not null&&p.DateOfBirth.HasValue&&!clear.Contains("date_of_birth",StringComparer.Ordinal))
            throw new ValidationException("Explicitly clear legacy date_of_birth when replacing it with calendar evidence; the original remains in history.");
        if (req.Name != null) p.Name = req.Name;
        if (req.Phone != null) p.Phone = WhatsappPhoneHelper.Normalize(req.Phone);
        if (req.Email != null) p.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (req.EmailNotificationsEnabled.HasValue) p.EmailNotificationsEnabled = req.EmailNotificationsEnabled.Value;
        if (req.Age.HasValue) p.Age = req.Age.Value;
        if (req.Gender.HasValue) p.Gender = req.Gender.Value;
        if (req.BloodGroup.HasValue) p.BloodGroup = req.BloodGroup.Value;
        if(req.DateOfBirth.HasValue)
        {
            p.DateOfBirth = DateTimeHelper.EnsureUtc(req.DateOfBirth);
            p.BirthDatePrecision="legacy_unspecified";p.BirthDateYear=null;p.BirthDateMonth=null;p.BirthDateDay=null;
        }
        if (req.Department != null) p.Department = req.Department;
        if (req.Status.HasValue) p.Status = req.Status.Value;
        if (req.AssignedStaffId.HasValue)
        {
            if (req.AssignedStaffId == Guid.Empty)
                throw new ValidationException("Select a valid staff user or explicitly clear the assignment.");
            _ = await db.QueryFirstOrDefaultAsync<User>("""
                SELECT "Id" FROM "Users" WHERE "Id"=@id AND "TenantId"=@TenantId
                    AND NOT "IsDeleted" AND "IsActive" FOR SHARE
                """, new { id = req.AssignedStaffId.Value }, ct: ct)
                ?? throw new NotFoundException("Staff user");
            p.AssignedStaffId = req.AssignedStaffId.Value;
        }
        if (req.Tags != null) p.Tags = req.Tags;
        if (req.Notes != null) p.Notes = req.Notes;
        if(req.FollowUpDate.HasValue)p.FollowUpDate = DateTimeHelper.EnsureUtc(req.FollowUpDate);
        if(req.Occupation is not null)p.Occupation = req.Occupation;
        if(req.MaritalStatus is not null)p.MaritalStatus = req.MaritalStatus;
        if(req.GovIdType is not null)p.GovIdType = req.GovIdType;
        if(req.GovIdNumber is not null)p.GovIdNumber = req.GovIdNumber;
        if(req.AddressLine1 is not null)p.AddressLine1 = req.AddressLine1;
        if(req.AddressLine2 is not null)p.AddressLine2 = req.AddressLine2;
        if(req.City is not null)p.City = req.City;
        if(req.State is not null)p.State = req.State;
        if(req.Pincode is not null)p.Pincode = req.Pincode;
        if(req.EmergencyContactName is not null)p.EmergencyContactName = req.EmergencyContactName;
        if(req.EmergencyContactPhone is not null)p.EmergencyContactPhone = WhatsappPhoneHelper.Normalize(req.EmergencyContactPhone);
        if(req.EmergencyContactRelation is not null)p.EmergencyContactRelation = req.EmergencyContactRelation;
        foreach(var field in clear)typeof(Patient).GetProperty(clearable[field])!.SetValue(p,null);
        if(clear.Contains("date_of_birth",StringComparer.Ordinal))
        {p.BirthDatePrecision="unknown";p.BirthDateYear=null;p.BirthDateMonth=null;p.BirthDateDay=null;}
        PatientDemographicRules.Apply(p,req.BirthDate);
        await SetDemographicSourceAsync(db,req.DemographicSource,ct);
        await db.UpdateAsync(p, ct: ct);
        var persisted=await db.GetByIdAsync<Patient>(id,ct:ct)??throw new NotFoundException("Patient");
        req.ResultETag=PatientConcurrency.Tag(persisted);
        },ct);
        await _audit.LogAsync("patient.update", "patient", id.ToString(), null, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.GetByIdAsync<Patient>(id, ct: ct)
            ?? throw new NotFoundException("Patient");
        p.IsDeleted = true;
        await _db.UpdateAsync(p, ct: ct);
        await _audit.LogAsync("patient.delete", "patient", id.ToString(), null, ct);
    }

    public Task<ImportResult> ImportCsvAsync(Stream csv, CancellationToken ct = default)
        =>throw new ConflictException("Use the reviewed /api/patient-imports preview and per-row decision workflow.");
    public Task<ImportResult> ImportExcelAsync(Stream stream, string fileExtension, CancellationToken ct = default)
        =>throw new ConflictException("Use the reviewed /api/patient-imports preview and per-row decision workflow.");
    private static PatientDetailDto MapDetail(Patient p) => new(
        p.Id, p.Name, p.Phone, p.Email, p.EmailNotificationsEnabled, p.Age, p.Gender, p.BloodGroup,
        p.DateOfBirth,
        p.AddressLine1, p.AddressLine2, p.City, p.State, p.Pincode,
        p.Department, p.Status, p.Tags, p.Notes, p.Occupation, p.MaritalStatus,
        p.GovIdType, p.GovIdNumber,
        p.EmergencyContactName, p.EmergencyContactPhone, p.EmergencyContactRelation,
        p.InquirySource, p.ReferralDoctor,
        p.LastContactAt, p.FollowUpDate,
        p.AiSummary, p.AiLeadScore,
        p.CreatedAt, p.UpdatedAt, p.SourceLeadId) {ConcurrencyTag=PatientConcurrency.Tag(p),BirthDate=new(p.BirthDatePrecision,p.BirthDateYear,p.BirthDateMonth,p.BirthDateDay)};

    private static LifestyleProfileDto MapLifestyle(LifestyleProfile l) => new(
        l.PatientId,
        l.AverageSleepHours, l.SleepQuality,
        l.WaterIntakeLitersPerDay, l.MealsPerDay, l.SkipsBreakfast,
        l.DietType, l.CuisinePreferences, l.DietaryRestrictions,
        l.CaffeineCupsPerDay, l.ConsumesProcessedFood, l.ConsumesSugaryDrinks,
        l.ExercisesRegularly, l.ExerciseMinutesPerWeek, l.ExerciseType,
        l.SmokingStatus, l.CigarettesPerDay,
        l.AlcoholConsumption, l.ChewsTobaccoOrPaan,
        l.Occupation, l.WorkSchedule, l.HighStressJob, l.StressLevel,
        l.MentalHealthConcerns, l.HasAnxietyOrDepression,
        l.LivingEnvironment, l.ExposureToPollution,
        l.AdditionalLifestyleNotes,
        l.LastUpdatedAt);

    private static PrescriptionDto MapPrescription(Prescription rx) => new(
        rx.Id, rx.PatientId, rx.VisitId, rx.DoctorUserId, rx.DoctorName,
        rx.PrescribedAt, rx.Diagnosis, rx.ChiefComplaint, rx.ClinicalNotes,
        rx.FollowUpAdvice, rx.NextVisitDate,
        rx.Items.Select(i => new PrescriptionItemDto(
            i.Id, i.DrugName, i.GenericName, i.Strength, i.Form, i.Route,
            i.Dosage, i.Frequency, i.Duration, i.Timing, i.Quantity,
            i.ReasonForPrescribing, i.PossibleSideEffects, i.PatientInstructions,
            i.IsContinuation, i.IsAcute)).ToList(),
        rx.Injections.Select(j => new InjectionDto(
            j.Id, j.Name, j.GenericName, j.Strength, j.Site, j.Route,
            j.AdministeredAt, j.AdministeredBy, j.AdministeredByUserId,
            j.BatchNumber, j.ExpiryDate,
            j.ReasonForInjection, j.AdverseReaction, j.Notes)).ToList());

    private static Task<int> SetDemographicSourceAsync(ICureFlowDbSession store,DemographicSourceInput? source,CancellationToken ct)
        =>store.ExecuteAsync("""
            SELECT set_config('app.demographic_source_type',@type,true),
              set_config('app.demographic_source_reference',@reference,true),
              set_config('app.demographic_reason',@reason,true)
            """,new{type=source?.SourceType??"local_application",reference=source?.SourceReference?.Trim()??"",reason=source?.Reason.Trim()??"Local application demographic command"},ct:ct);    private async Task RequirePatientWriteAsync(ICureFlowDbSession store,string capability,CancellationToken ct)
    {
        if(!_tenant.IsAuthenticated||_tenant.IsPlatformUser||_tenant.TenantId==Guid.Empty||!_tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{Id=_tenant.UserId},ct:ct);
        if(actor is null||!(await _permissions.GetPermissionCodesAsync(actor,ct,store)).Contains(capability,StringComparer.Ordinal))throw new ForbiddenException();
    }}
