using System.Text.Json;
using ExcelDataReader;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<PatientService> _logger;

    public PatientService(
        ICureFlowDbSession db,
        IAuditService audit,
        INotificationPublisher notifications,
        ITenantContext tenant,
        ILogger<PatientService> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
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

    public async Task<ImportResult> ImportCsvAsync(Stream csv, CancellationToken ct = default)
    {
        using var reader = new StreamReader(csv);
        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine == null) return new ImportResult(0, 0, 0, []);

        var header = headerLine.Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();

        // Flexible header matching
        int FindCol(string[] aliases)
        {
            for (int i = 0; i < header.Length; i++)
                if (aliases.Any(a => header[i].Contains(a, StringComparison.OrdinalIgnoreCase))) return i;
            return -1;
        }
        int nameIx = FindCol(["patient name", "name", "full name"]);
        int phoneIx = FindCol(["phone", "mobile", "mob", "contact", "mobile number", "phone number", "contact number"]);
        int ageIx = FindCol(["age", "years"]);
        int genderIx = FindCol(["gender", "sex"]);
        int deptIx = FindCol(["department", "dept", "specialty"]);
        int srcIx = FindCol(["source", "inquiry source", "inquirysource"]);
        int tagsIx = FindCol(["tags", "tag", "category"]);
        int notesIx = FindCol(["notes", "note", "remarks", "comment"]);

        if (nameIx < 0 || phoneIx < 0)
        {
            _logger.LogWarning("[ImportCSV] Could not find required Name/Phone columns. Header: {Header}", headerLine);
            return new ImportResult(0, 0, 0, [new ImportSkipRecord(0, null, null, $"Header row missing Name/Phone columns. Found: {headerLine}")]);
        }

        int inserted = 0, skipped = 0, blankRows = 0, rowNum = 1;
        var skipLog = new List<ImportSkipRecord>();
        var seenPatients = new HashSet<(string Phone, string Name, int? Age, Gender Gender)>();

        await _db.TransactionAsync(async tx =>
        {
            while (!reader.EndOfStream)
            {
                rowNum++;
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(line)) { blankRows++; continue; }
                var c = line.Split(',');

                var name = nameIx < c.Length ? c[nameIx].Trim() : null;
                var phoneRaw = phoneIx < c.Length ? c[phoneIx].Trim() : null;
                int? age = null;
                if (ageIx >= 0 && ageIx < c.Length)
                {
                    var ageRaw = c[ageIx].Trim();
                    if (ageRaw.EndsWith(".0")) ageRaw = ageRaw[..^2];
                    if (int.TryParse(ageRaw, out var parsedAge)) age = parsedAge;
                    else if (double.TryParse(ageRaw, out var parsedDouble)) age = (int)parsedDouble;
                }

                if (string.IsNullOrEmpty(name))
                {
                    var reason = "Missing Name";
                    _logger.LogWarning("[ImportCSV] Row {Row} skipped – {Reason}: name='{Name}' phone='{Phone}'", rowNum, reason, name, phoneRaw);
                    skipLog.Add(new ImportSkipRecord(rowNum, name, phoneRaw, reason));
                    skipped++; continue;
                }

                var phone = WhatsappPhoneHelper.Normalize(phoneRaw);
                if (!string.IsNullOrWhiteSpace(phoneRaw) && string.IsNullOrEmpty(phone))
                {
                    _logger.LogWarning("[ImportCSV] Row {Row} skipped – Invalid phone after normalize: '{PhoneRaw}'", rowNum, phoneRaw);
                    skipLog.Add(new ImportSkipRecord(rowNum, name, phoneRaw, $"Invalid phone number: '{phoneRaw}'"));
                    skipped++; continue;
                }

                var normalizedName = name.Trim();
                var gender = genderIx >= 0 && genderIx < c.Length && Enum.TryParse<Gender>(c[genderIx].Trim(), true, out var g) ? g : Gender.Unknown;
                var fileKey = (Phone: phone, Name: normalizedName.ToLowerInvariant(), Age: age, Gender: gender);
                if (!seenPatients.Add(fileKey))
                {
                    _logger.LogWarning("[ImportCSV] Row {Row} skipped – Duplicate patient in file: name='{Name}' phone='{Phone}' age='{Age}' gender='{Gender}'", rowNum, name, phone, age?.ToString() ?? "null", gender);
                    skipLog.Add(new ImportSkipRecord(rowNum, name, phoneRaw, $"Duplicate patient in file: {normalizedName}, {phone}, {age?.ToString() ?? "null"}, {gender}"));
                    skipped++; continue;
                }

                var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
                var exists = await tx.QueryFirstOrDefaultAsync<Patient>(
                    $"""SELECT * FROM "Patients" WHERE COALESCE("Phone", '') = @phone AND lower("Name") = lower(@name) AND ("Age" IS NOT DISTINCT FROM @age) AND "Gender" = @gender AND {patientWhere} LIMIT 1""",
                    new { phone, name = normalizedName, age, gender }, ct: ct);
                if (exists != null)
                {
                    _logger.LogWarning("[ImportCSV] Row {Row} skipped – Already exists in database: name='{Name}' phone='{Phone}' age='{Age}' gender='{Gender}'", rowNum, name, phone, age?.ToString() ?? "null", gender);
                    skipLog.Add(new ImportSkipRecord(rowNum, name, phoneRaw, "Already exists in database (same Name, Phone, Age, and Gender)"));
                    skipped++; continue;
                }

                var p = new Patient
                {
                    Name = name,
                    Phone = phone,
                    Age = age,
                    Gender = gender,
                    Department = deptIx >= 0 && deptIx < c.Length ? c[deptIx].Trim() : null,
                    InquirySource = srcIx >= 0 && srcIx < c.Length && !string.IsNullOrWhiteSpace(c[srcIx]) ? c[srcIx].Trim() : "csv_import",
                    Tags = tagsIx >= 0 && tagsIx < c.Length ? c[tagsIx].Split(';').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() : new(),
                    Notes = notesIx >= 0 && notesIx < c.Length ? c[notesIx].Trim() : null,
                };
                await tx.InsertAsync(p, ct: ct);
                _logger.LogInformation("[ImportCSV] Row {Row} inserted: name='{Name}' phone='{Phone}'", rowNum, name, phone);
                inserted++;
            }
        }, ct);

        _logger.LogInformation("[ImportCSV] Complete: {Inserted} inserted, {Skipped} skipped, {Blank} blank rows", inserted, skipped, blankRows);
        await _audit.LogAsync("patient.import_csv", "patient", null, new { inserted, skipped, blankRows }, ct);
        return new ImportResult(inserted, skipped, blankRows, skipLog);
    }

    public async Task<ImportResult> ImportExcelAsync(Stream stream, string fileExtension, CancellationToken ct = default)
    {
        var ext = fileExtension?.ToLowerInvariant();
        if (ext == ".csv")
        {
            return await ImportCsvAsync(stream, ct);
        }

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var reader = ExcelReaderFactory.CreateReader(stream);

        // Read ALL rows with UseHeaderRow=false so we have full control over header detection
        var result = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = (_) => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        if (result.Tables.Count == 0) return new ImportResult(0, 0, 0, []);
        var table = result.Tables[0];
        if (table.Rows.Count < 2) return new ImportResult(0, 0, 0, []);

        // --- Flexible header detection: scan up to first 5 rows for a row that has "name" and phone-like col ---
        int headerRowIndex = -1;
        int nameIx = -1, phoneIx = -1, emailIx = -1, ageIx = -1, genderIx = -1,
            deptIx = -1, srcIx = -1, tagsIx = -1, notesIx = -1;

        static bool MatchesColumn(string col, string[] keywords)
            => keywords.Any(k => col.Contains(k, StringComparison.OrdinalIgnoreCase));

        for (int rowIdx = 0; rowIdx < Math.Min(5, table.Rows.Count); rowIdx++)
        {
            var row = table.Rows[rowIdx];
            int tempName = -1, tempPhone = -1;
            for (int c = 0; c < table.Columns.Count; c++)
            {
                var cell = row[c]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(cell)) continue;

                // Name column aliases
                if (tempName < 0 && MatchesColumn(cell,
                    ["patient name", "patientname", "name", "full name", "fullname", "नाम"]))
                    tempName = c;

                // Phone column aliases
                if (tempPhone < 0 && MatchesColumn(cell,
                    ["phone", "mobile", "mob", "contact", "cell", "phone no", "mobile no", "ph no",
                     "phone number", "mobile number", "contact number", "फ़ोन", "मोबाइल"]))
                    tempPhone = c;
            }

            // This row looks like a header
            if (tempName >= 0 && tempPhone >= 0)
            {
                headerRowIndex = rowIdx;
                nameIx = tempName;
                phoneIx = tempPhone;

                // Now map remaining columns
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    if (c == nameIx || c == phoneIx) continue;
                    var cell = row[c]?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(cell)) continue;

                    if (emailIx < 0 && MatchesColumn(cell, ["email", "e-mail", "mail", "ईमेल"]))
                        emailIx = c;
                    else if (ageIx < 0 && MatchesColumn(cell, ["age", "years", "yr", "उम्र", "आयु"]))
                        ageIx = c;
                    else if (genderIx < 0 && MatchesColumn(cell,
                        ["gender", "sex", "m/f", "m/f/o", "लिंग"]))
                        genderIx = c;
                    else if (deptIx < 0 && MatchesColumn(cell,
                        ["department", "dept", "specialty", "speciality", "विभाग"]))
                        deptIx = c;
                    else if (srcIx < 0 && MatchesColumn(cell,
                        ["source", "inquiry source", "inquirysource", "inquiry_source", "referred by", "referral"]))
                        srcIx = c;
                    else if (tagsIx < 0 && MatchesColumn(cell, ["tags", "tag", "category", "label"]))
                        tagsIx = c;
                    else if (notesIx < 0 && MatchesColumn(cell,
                        ["notes", "note", "remarks", "remark", "comment", "comments", "description"]))
                        notesIx = c;
                }
                break;
            }
        }

        // Fallback: if no header found but we have columns, treat row 0 as header by position
        // (Name=col0, Phone=col1)
        if (headerRowIndex < 0 && table.Columns.Count >= 2)
        {
            headerRowIndex = 0;
            nameIx = 0;
            phoneIx = 1;
            if (table.Columns.Count > 2) ageIx = 2;
            if (table.Columns.Count > 3) genderIx = 3;
            if (table.Columns.Count > 4) deptIx = 4;
        }

        if (headerRowIndex < 0 || nameIx < 0 || phoneIx < 0)
        {
            _logger.LogWarning("[ImportExcel] Could not detect Name/Phone header columns in first 5 rows");
            return new ImportResult(0, 0, 0, [new ImportSkipRecord(0, null, null, "Could not find Name and Phone columns in file header")]);
        }

        _logger.LogInformation("[ImportExcel] Header found at row {HeaderRow}. nameIx={N} phoneIx={P} ageIx={A} genderIx={G} deptIx={D}",
            headerRowIndex, nameIx, phoneIx, ageIx, genderIx, deptIx);

        int inserted = 0, skipped = 0, blankRows = 0;
        var skipLog = new List<ImportSkipRecord>();
        var seenPatients = new HashSet<(string Phone, string Name, int? Age, Gender Gender)>();

        await _db.TransactionAsync(async tx =>
        {
            for (int rowIdx = headerRowIndex + 1; rowIdx < table.Rows.Count; rowIdx++)
            {
                int excelRow = rowIdx + 1; // 1-based for user display
                var row = table.Rows[rowIdx];

                // Skip fully blank rows
                var isBlank = true;
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    if (!string.IsNullOrWhiteSpace(row[c]?.ToString())) { isBlank = false; break; }
                }
                if (isBlank) { blankRows++; continue; }

                var name = row[nameIx]?.ToString()?.Trim();
                var phoneRaw = row[phoneIx]?.ToString()?.Trim();
                int? parsedAge = null;
                if (ageIx >= 0 && row[ageIx] != null)
                {
                    var ageRaw = row[ageIx].ToString()?.Trim() ?? "";
                    if (ageRaw.EndsWith(".0")) ageRaw = ageRaw[..^2];
                    if (int.TryParse(ageRaw, out var a)) parsedAge = a;
                    else if (double.TryParse(ageRaw, out var ad)) parsedAge = (int)ad;
                }

                if (string.IsNullOrEmpty(name))
                {
                    var reason = "Missing Name";
                    _logger.LogWarning("[ImportExcel] Row {Row} skipped – {Reason}: name='{Name}' phone='{Phone}'", excelRow, reason, name, phoneRaw);
                    skipLog.Add(new ImportSkipRecord(excelRow, name, phoneRaw, reason));
                    skipped++; continue;
                }

                // Strip Excel numeric trailing decimal (9876543210.0 → 9876543210)
                if (!string.IsNullOrEmpty(phoneRaw) && phoneRaw.EndsWith(".0")) phoneRaw = phoneRaw[..^2];

                // Handle scientific notation (e.g. 9.87654321E+09)
                if (!string.IsNullOrEmpty(phoneRaw) && phoneRaw.Contains('E', StringComparison.OrdinalIgnoreCase)
                    && double.TryParse(phoneRaw, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var phoneDbl))
                {
                    phoneRaw = ((long)phoneDbl).ToString();
                }

                var phone = WhatsappPhoneHelper.Normalize(phoneRaw ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(phoneRaw) && string.IsNullOrEmpty(phone))
                {
                    _logger.LogWarning("[ImportExcel] Row {Row} skipped – Invalid phone after normalize: raw='{Raw}'", excelRow, phoneRaw);
                    skipLog.Add(new ImportSkipRecord(excelRow, name, phoneRaw, $"Invalid phone number: '{phoneRaw}'"));
                    skipped++; continue;
                }

                var normalizedName = name.Trim();
                var gender = Gender.Unknown;
                if (genderIx >= 0 && row[genderIx] != null)
                {
                    var g = row[genderIx].ToString()?.Trim()?.ToLowerInvariant() ?? "";
                    if (g is "m" or "male" or "पुरुष") gender = Gender.Male;
                    else if (g is "f" or "female" or "महिला" or "lady") gender = Gender.Female;
                    else if (g is "o" or "other") gender = Gender.Other;
                }
                var fileKey = (Phone: phone, Name: normalizedName.ToLowerInvariant(), Age: parsedAge, Gender: gender);
                if (!seenPatients.Add(fileKey))
                {
                    _logger.LogWarning("[ImportExcel] Row {Row} skipped – Duplicate patient in file: name='{Name}' phone='{Phone}' age='{Age}' gender='{Gender}'", excelRow, name, phone, parsedAge?.ToString() ?? "null", gender);
                    skipLog.Add(new ImportSkipRecord(excelRow, name, phoneRaw, $"Duplicate patient in file: {normalizedName}, {phone}, {parsedAge?.ToString() ?? "null"}, {gender}"));
                    skipped++; continue;
                }

                // Check if patient already exists in DB (current tenant)
                var patientWhere = SqlFragments.WhereActive<Patient>(ignoreTenant: false);
                var exists = await tx.QueryFirstOrDefaultAsync<Patient>(
                    $"""SELECT * FROM "Patients" WHERE COALESCE("Phone", '') = @phone AND lower("Name") = lower(@name) AND ("Age" IS NOT DISTINCT FROM @age) AND "Gender" = @gender AND {patientWhere} LIMIT 1""",
                    new { phone, name = normalizedName, age = parsedAge, gender }, ct: ct);
                if (exists != null)
                {
                    _logger.LogWarning("[ImportExcel] Row {Row} skipped – Already exists in DB: name='{Name}' phone='{Phone}' age='{Age}' gender='{Gender}'", excelRow, name, phone, parsedAge?.ToString() ?? "null", gender);
                    skipLog.Add(new ImportSkipRecord(excelRow, name, phoneRaw, "Already exists in database (same Name, Phone, Age, and Gender)"));
                    skipped++; continue;
                }

                var p = new Patient
                {
                    Name = name,
                    Phone = phone,
                    Email = emailIx >= 0 ? row[emailIx]?.ToString()?.Trim() : null,
                    Age = parsedAge,
                    Gender = gender,
                    Department = deptIx >= 0 ? row[deptIx]?.ToString()?.Trim() : null,
                    InquirySource = srcIx >= 0 && !string.IsNullOrWhiteSpace(row[srcIx]?.ToString())
                        ? row[srcIx]?.ToString()?.Trim()!
                        : "excel_import",
                    Tags = tagsIx >= 0 && !string.IsNullOrWhiteSpace(row[tagsIx]?.ToString())
                        ? row[tagsIx]?.ToString()!.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(t => t.Trim()).ToList()!
                        : new List<string>(),
                    Notes = notesIx >= 0 ? row[notesIx]?.ToString()?.Trim() : null,
                };
                await tx.InsertAsync(p, ct: ct);
                _logger.LogInformation("[ImportExcel] Row {Row} inserted: name='{Name}' phone='{Phone}'", excelRow, name, phone);
                inserted++;
            }
        }, ct);

        _logger.LogInformation("[ImportExcel] Complete: {Inserted} inserted, {Skipped} skipped, {Blank} blank rows. Skip reasons: {SkipLog}",
            inserted, skipped, blankRows,
            string.Join(" | ", skipLog.Take(20).Select(s => $"R{s.RowNumber}:{s.Reason}")));

        await _audit.LogAsync("patient.import_excel", "patient", null, new { inserted, skipped, blankRows }, ct);
        return new ImportResult(inserted, skipped, blankRows, skipLog);
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
