using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Notifications;
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

    public PatientService(
        ICureFlowDbSession db,
        IAuditService audit,
        INotificationPublisher notifications,
        ITenantContext tenant)
    {
        _db = db;
        _audit = audit;
        _notifications = notifications;
        _tenant = tenant;
    }

    public async Task<Guid> CreateAsync(CreatePatientRequest req, CancellationToken ct = default)
    {
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
        await _db.InsertAsync(p, ct: ct);
        await _audit.LogAsync("patient.create", "patient", p.Id.ToString(), null, ct);

        await _notifications.PublishAsync(new NotificationPublishRequest(
            NotificationTypeCodes.PatientCreated,
            "New patient registered",
            $"{p.Name.Trim()} was added to the patient registry",
            NotificationSeverity.Info,
            EntityType: "patient",
            EntityId: p.Id,
            ActionUrl: $"/patients/{p.Id}",
            CreatorUserId: _tenant.UserId), ct);

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
            """SELECT * FROM "ClinicalNotes" WHERE "PatientId" = @patientId ORDER BY "CreatedAt" DESC LIMIT 20""",
            new { patientId = id }, ct: ct);
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
        int page, int pageSize, CancellationToken ct = default)
    {
        var (normalizedPage, normalizedPageSize, skip) = Pagination.Normalize(page, pageSize);
        var filters = new List<string> { SqlFragments.WhereActive<Patient>(ignoreTenant: false) };
        var param = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(q))
        {
            filters.Add("(\"Name\" ILIKE @q OR \"Phone\" ILIKE @q OR (\"Email\" IS NOT NULL AND \"Email\" ILIKE @q))");
            param["q"] = $"%{q}%";
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
        var rows = await _db.QueryAsync<Patient>(
            $"""
            SELECT * FROM "Patients"{where}
            ORDER BY "CreatedAt" DESC
            OFFSET @skip LIMIT @take
            """, param, ct: ct);

        var items = rows.Select(p => new PatientSummaryDto(p.Id, p.Name, p.Phone, p.Email, p.Age, p.Gender,
            p.Department, p.Status, p.Tags, p.InquirySource, p.CreatedAt, p.LastContactAt)).ToList();

        return new PagedResult<PatientSummaryDto>
        {
            Items = items,
            Total = total,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
        };
    }

    public async Task UpdateAsync(Guid id, UpdatePatientRequest req, CancellationToken ct = default)
    {
        var p = await _db.GetByIdAsync<Patient>(id, ct: ct)
            ?? throw new NotFoundException("Patient");
        if (req.Name != null) p.Name = req.Name;
        if (req.Phone != null) p.Phone = WhatsappPhoneHelper.Normalize(req.Phone);
        if (req.Email != null) p.Email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (req.EmailNotificationsEnabled.HasValue) p.EmailNotificationsEnabled = req.EmailNotificationsEnabled.Value;
        if (req.Age.HasValue) p.Age = req.Age.Value;
        if (req.Gender.HasValue) p.Gender = req.Gender.Value;
        if (req.BloodGroup.HasValue) p.BloodGroup = req.BloodGroup.Value;
        p.DateOfBirth = DateTimeHelper.EnsureUtc(req.DateOfBirth);
        if (req.Department != null) p.Department = req.Department;
        if (req.Status.HasValue) p.Status = req.Status.Value;
        if (req.AssignedStaffId.HasValue) p.AssignedStaffId = req.AssignedStaffId.Value;
        if (req.Tags != null) p.Tags = req.Tags;
        if (req.Notes != null) p.Notes = req.Notes;
        p.FollowUpDate = DateTimeHelper.EnsureUtc(req.FollowUpDate);
        p.Occupation = req.Occupation;
        p.MaritalStatus = req.MaritalStatus;
        p.GovIdType = req.GovIdType;
        p.GovIdNumber = req.GovIdNumber;
        p.AddressLine1 = req.AddressLine1;
        p.AddressLine2 = req.AddressLine2;
        p.City = req.City;
        p.State = req.State;
        p.Pincode = req.Pincode;
        p.EmergencyContactName = req.EmergencyContactName;
        p.EmergencyContactPhone = req.EmergencyContactPhone != null
            ? WhatsappPhoneHelper.Normalize(req.EmergencyContactPhone)
            : null;
        p.EmergencyContactRelation = req.EmergencyContactRelation;
        await _db.UpdateAsync(p, ct: ct);
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

    public async Task<(int inserted, int skipped)> ImportCsvAsync(Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv);
        var header = (await reader.ReadLineAsync(ct))?.Split(',').Select(h => h.Trim().ToLower()).ToArray();
        if (header == null || !header.Contains("name") || !header.Contains("phone")) return (0, 0);

        int idx(string col) => Array.IndexOf(header, col);
        int nameIx = idx("name"), phoneIx = idx("phone"), ageIx = idx("age"),
            genderIx = idx("gender"), deptIx = idx("department"), srcIx = idx("source"), tagsIx = idx("tags");

        int inserted = 0, skipped = 0;
        await _db.TransactionAsync(async tx =>
        {
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) continue;
                var c = line.Split(',');
                if (c.Length < 2) continue;
                var name = c[nameIx].Trim();
                var phone = WhatsappPhoneHelper.Normalize(c[phoneIx].Trim());
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone)) { skipped++; continue; }

                var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
                var exists = await tx.QueryFirstOrDefaultAsync<Patient>(
                    $"""SELECT * FROM "Patients" WHERE "Phone" = @phone AND {patientWhere} LIMIT 1""",
                    new { phone }, ct: ct);
                if (exists != null) { skipped++; continue; }

                var p = new Patient
                {
                    Name = name,
                    Phone = phone,
                    Age = ageIx >= 0 && int.TryParse(c[ageIx], out var a) ? a : null,
                    Gender = genderIx >= 0 && Enum.TryParse<Gender>(c[genderIx], true, out var g) ? g : Gender.Unknown,
                    Department = deptIx >= 0 ? c[deptIx] : null,
                    InquirySource = srcIx >= 0 ? c[srcIx] : "csv_import",
                    Tags = tagsIx >= 0 ? c[tagsIx].Split(';').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() : new(),
                };
                await tx.InsertAsync(p, ct: ct);
                inserted++;
            }
        }, ct);

        await _audit.LogAsync("patient.import_csv", "patient", null, new { inserted, skipped }, ct);
        return (inserted, skipped);
    }

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
        p.CreatedAt, p.UpdatedAt);

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
}
