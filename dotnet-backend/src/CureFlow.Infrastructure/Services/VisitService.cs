using System.Text;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.Services;

public class VisitService : IVisitService
{
    private const long MaxLabReportBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedLabReportContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/jpeg", "image/png",
    };
    private static readonly HashSet<string> AllowedLabReportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".jpg", ".jpeg", ".png",
    };

    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly StorageOptions _storage;

    public VisitService(ICureFlowDbSession db, ITenantContext tenant, IAuditService audit, IOptions<StorageOptions> storage)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
        _storage = storage.Value;
    }

    public async Task<Guid> CreateAsync(CreateVisitRequest req, CancellationToken ct = default)
    {
        if (req.AppointmentId.HasValue)
        {
            var visitWhere = SqlFragments.WhereActive<Visit>(ignoreTenant: false);
            var existing = await _db.QueryFirstOrDefaultAsync<Visit>(
                $"""SELECT * FROM "Visits" WHERE "AppointmentId" = @appointmentId AND {visitWhere} ORDER BY "VisitDate" DESC LIMIT 1""",
                new { appointmentId = req.AppointmentId.Value },
                ct: ct);
            if (existing != null)
                return existing.Id;
        }

        var patient = await _db.GetByIdAsync<Patient>(req.PatientId, ct: ct)
            ?? throw new NotFoundException("Patient");

        var doctorUserId = req.DoctorUserId ?? _tenant.UserId ?? Guid.Empty;
        var doctor = await _db.GetByIdAsync<User>(doctorUserId, ct: ct);
        var doctorName = doctor?.Name ?? "Doctor";

        var visit = new Visit
        {
            PatientId = req.PatientId,
            AppointmentId = req.AppointmentId,
            DoctorUserId = doctorUserId,
            DoctorName = doctorName,
            Department = req.Department ?? patient.Department,
            VisitDate = DateTime.UtcNow,
            VisitType = req.VisitType ?? "outpatient",
            ChiefComplaint = req.Symptoms,
            Symptoms = req.Symptoms,
            Diagnosis = req.Diagnosis,
            DoctorNotes = req.DoctorNotes,
            FollowUpAdvice = req.FollowUpAdvice,
            FollowUpDate = DateTimeHelper.EnsureUtc(req.FollowUpDate),
            Status = VisitStatus.InProgress,
        };
        await _db.InsertAsync(visit, ct: ct);
        await _audit.LogAsync("visit.create", "visit", visit.Id.ToString(), new { req.PatientId }, ct);
        return visit.Id;
    }

    public async Task<VisitDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var visit = await _db.GetByIdAsync<Visit>(id, ct: ct)
            ?? throw new NotFoundException("Visit");

        var vitals = await _db.QueryAsync<VitalSigns>(
            """SELECT * FROM "VitalSigns" WHERE "VisitId" = @visitId ORDER BY "MeasuredAt" DESC""",
            new { visitId = id }, ct: ct);
        var notes = await _db.QueryAsync<ClinicalNote>(
            """SELECT * FROM "ClinicalNotes" WHERE "VisitId" = @visitId ORDER BY "CreatedAt" DESC""",
            new { visitId = id }, ct: ct);
        var rxList = await PrescriptionGraphLoader.LoadAsync(
            _db, @"""VisitId"" = @visitId", new { visitId = id },
            """ORDER BY "PrescribedAt" DESC""", ct);

        return new VisitDetailDto(
            visit.Id, visit.PatientId, visit.AppointmentId, visit.DoctorUserId, visit.DoctorName,
            visit.Department, visit.VisitDate, visit.EndedAt, visit.Status, visit.VisitType,
            visit.Symptoms, visit.Diagnosis, visit.DoctorNotes,
            visit.FollowUpAdvice, visit.FollowUpDate, visit.AttachmentsJson,
            vitals.Select(MapVital).ToList(),
            notes.Select(MapNote).ToList(),
            rxList.Select(MapRx).ToList());
    }

    public async Task<IReadOnlyList<VisitSummaryDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<Visit>(
            """SELECT * FROM "Visits" WHERE "PatientId" = @patientId ORDER BY "VisitDate" DESC""",
            new { patientId }, ct: ct);
        return rows.Select(v => new VisitSummaryDto(
            v.Id, v.PatientId, v.DoctorUserId, v.DoctorName,
            v.Department, v.VisitDate, v.Status, v.VisitType,
            v.Symptoms, v.Diagnosis, v.FollowUpDate)).ToList();
    }

    public async Task<IReadOnlyList<VisitChartCardDto>> GetVisitChartAsync(Guid patientId, CancellationToken ct = default)
    {
        if (await _db.GetByIdAsync<Patient>(patientId, ct: ct) is null)
            throw new NotFoundException("Patient");

        var visits = (await _db.QueryAsync<Visit>(
            """SELECT * FROM "Visits" WHERE "PatientId" = @patientId ORDER BY "VisitDate" DESC""",
            new { patientId }, ct: ct)).ToList();

        var vitals = (await _db.QueryAsync<VitalSigns>(
            """SELECT * FROM "VitalSigns" WHERE "PatientId" = @patientId ORDER BY "MeasuredAt" DESC""",
            new { patientId }, ct: ct)).ToList();
        var notes = (await _db.QueryAsync<ClinicalNote>(
            """SELECT * FROM "ClinicalNotes" WHERE "PatientId" = @patientId ORDER BY "CreatedAt" DESC""",
            new { patientId }, ct: ct)).ToList();
        var prescriptions = (await PrescriptionGraphLoader.LoadAsync(
            _db, @"""PatientId"" = @patientId", new { patientId },
            """ORDER BY "PrescribedAt" DESC""", ct)).ToList();
        var documents = (await _db.QueryAsync<PatientDocument>(
            """SELECT * FROM "PatientDocuments" WHERE "PatientId" = @patientId ORDER BY "CapturedAt" DESC""",
            new { patientId }, ct: ct)).ToList();

        var visitById = visits.ToDictionary(v => v.Id);
        var cardsByKey = new Dictionary<string, VisitChartCardBuilder>(StringComparer.Ordinal);

        foreach (var visit in visits)
        {
            var key = VisitDateKey(visit.VisitDate) + "|" + visit.Id.ToString("N");
            cardsByKey[key] = new VisitChartCardBuilder
            {
                VisitId = visit.Id,
                VisitDate = visit.VisitDate,
                VisitDateKey = VisitDateKey(visit.VisitDate),
                DoctorName = visit.DoctorName,
                Department = visit.Department,
                ChiefComplaint = visit.ChiefComplaint ?? visit.Symptoms,
                Diagnosis = visit.Diagnosis,
                DoctorNotes = visit.DoctorNotes,
                Status = visit.Status.ToString(),
            };
        }

        void EnsureDayCard(DateTime when, string? doctorName = null)
        {
            var day = VisitDateKey(when);
            var key = day + "|day";
            if (cardsByKey.ContainsKey(key)) return;
            // Prefer attaching to a visit on the same UTC calendar day when one exists.
            var sameDayVisit = visits.FirstOrDefault(v => VisitDateKey(v.VisitDate) == day);
            if (sameDayVisit != null) return;
            cardsByKey[key] = new VisitChartCardBuilder
            {
                VisitId = null,
                VisitDate = when,
                VisitDateKey = day,
                DoctorName = doctorName,
                Status = "Record",
            };
        }

        VisitChartCardBuilder? ResolveCard(Guid? visitId, DateTime when, string? doctorName = null)
        {
            if (visitId.HasValue && visitById.TryGetValue(visitId.Value, out var visit))
            {
                var key = VisitDateKey(visit.VisitDate) + "|" + visit.Id.ToString("N");
                return cardsByKey[key];
            }

            var day = VisitDateKey(when);
            var sameDayVisit = visits.FirstOrDefault(v => VisitDateKey(v.VisitDate) == day);
            if (sameDayVisit != null)
            {
                var key = day + "|" + sameDayVisit.Id.ToString("N");
                return cardsByKey[key];
            }

            EnsureDayCard(when, doctorName);
            return cardsByKey[day + "|day"];
        }

        foreach (var v in vitals)
            ResolveCard(v.VisitId, v.MeasuredAt, v.RecordedByName)?.Vitals.Add(MapVital(v));
        foreach (var n in notes)
            ResolveCard(n.VisitId, n.CreatedAt, n.AuthorName)?.Notes.Add(MapNote(n));
        foreach (var rx in prescriptions)
            ResolveCard(rx.VisitId, rx.PrescribedAt, rx.DoctorName)?.Prescriptions.Add(MapRx(rx));
        foreach (var d in documents)
            ResolveCard(d.VisitId, d.CapturedAt, d.UploadedByName)?.PaperNotes.Add(MapDocument(d));

        return cardsByKey.Values
            .OrderByDescending(c => c.VisitDate)
            .Select(c => c.ToDto())
            .ToList();
    }

    public async Task UpdateAsync(Guid id, UpdateVisitRequest req, CancellationToken ct = default)
    {
        var visit = await _db.GetByIdAsync<Visit>(id, ct: ct)
            ?? throw new NotFoundException("Visit");
        if (req.Symptoms != null) visit.Symptoms = req.Symptoms;
        if (req.Diagnosis != null) visit.Diagnosis = req.Diagnosis;
        if (req.DoctorNotes != null) visit.DoctorNotes = req.DoctorNotes;
        if (req.FollowUpAdvice != null) visit.FollowUpAdvice = req.FollowUpAdvice;
        if (req.FollowUpDate.HasValue) visit.FollowUpDate = DateTimeHelper.EnsureUtc(req.FollowUpDate);
        if (req.Status.HasValue) visit.Status = req.Status.Value;
        await _db.UpdateAsync(visit, ct: ct);
    }

    public async Task CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var visit = await _db.GetByIdAsync<Visit>(id, ct: ct)
            ?? throw new NotFoundException("Visit");
        visit.Status = VisitStatus.Completed;
        visit.EndedAt = DateTime.UtcNow;
        await _db.UpdateAsync(visit, ct: ct);
    }

    public async Task<IReadOnlyList<TimelineEntryDto>> GetTimelineAsync(Guid patientId, CancellationToken ct = default)
    {
        var entries = new List<TimelineEntryDto>();

        var visits = await _db.QueryAsync<Visit>(
            """SELECT * FROM "Visits" WHERE "PatientId" = @patientId""",
            new { patientId }, ct: ct);
        entries.AddRange(visits.Select(v => new TimelineEntryDto(
            "visit", v.VisitDate, v.Id,
            $"Visit — {v.DoctorName}",
            v.Diagnosis ?? v.Symptoms,
            new { v.Id, v.Status, v.VisitType, v.Department })));

        var vitals = await _db.QueryAsync<VitalSigns>(
            """SELECT * FROM "VitalSigns" WHERE "PatientId" = @patientId""",
            new { patientId }, ct: ct);
        entries.AddRange(vitals.Select(v => new TimelineEntryDto(
            "vitals", v.MeasuredAt, v.Id,
            "Vital Signs",
            v.SystolicBp.HasValue ? $"BP {v.SystolicBp}/{v.DiastolicBp}" : v.Notes,
            new { v.VisitId, v.RecordedByName })));

        var notes = await _db.QueryAsync<ClinicalNote>(
            """SELECT * FROM "ClinicalNotes" WHERE "PatientId" = @patientId""",
            new { patientId }, ct: ct);
        entries.AddRange(notes.Select(n => new TimelineEntryDto(
            "clinical_note", n.CreatedAt, n.Id,
            $"Clinical Note — {n.NoteType}",
            n.Assessment ?? n.Subjective,
            new { n.VisitId, n.AuthorName })));

        var rxList = await PrescriptionGraphLoader.LoadAsync(
            _db, @"""PatientId"" = @patientId", new { patientId }, orderByLimit: null, ct: ct);
        entries.AddRange(rxList.Select(r => new TimelineEntryDto(
            "prescription", r.PrescribedAt, r.Id,
            $"Prescription — {r.DoctorName}",
            r.Diagnosis ?? $"{r.Items.Count} medication(s)",
            new { r.VisitId, r.DoctorName })));

        var rxIds = rxList.Select(r => r.Id).ToArray();
        if (rxIds.Length > 0)
        {
            var injections = await _db.QueryAsync<Injection>(
                """SELECT * FROM "Injections" WHERE "PrescriptionId" = ANY(@rxIds)""",
                new { rxIds }, ct: ct);
            entries.AddRange(injections.Select(j => new TimelineEntryDto(
                "injection", j.AdministeredAt, j.Id,
                $"Injection — {j.Name}",
                j.ReasonForInjection,
                new { j.AdministeredBy, j.AdministeredByUserId })));
        }

        var apptWhere = SqlFragments.WhereActive<Appointment>(ignoreTenant: false);
        var appts = await _db.QueryAsync<Appointment>(
            $"""SELECT * FROM "Appointments" WHERE {apptWhere} AND "PatientId" = @patientId""",
            new { patientId }, ct: ct);
        entries.AddRange(appts.Select(a => new TimelineEntryDto(
            "appointment", a.ScheduledAt, a.Id,
            $"Appointment — {a.DoctorName}",
            a.ChiefComplaint ?? a.Department,
            new { a.Status, a.Department })));

        var labs = await _db.QueryAsync<LabReport>(
            """SELECT * FROM "LabReports" WHERE "PatientId" = @patientId""",
            new { patientId }, ct: ct);
        entries.AddRange(labs.Select(l => new TimelineEntryDto(
            "lab_report", l.ReportedAt ?? l.CreatedAt, l.Id,
            $"Lab — {l.TestName}",
            l.Interpretation,
            new { l.VisitId, l.FileUrl, l.IsAbnormal })));

        return entries.OrderByDescending(e => e.OccurredAt).ToList();
    }

    public async Task<string> GetPrescriptionPrintHtmlAsync(Guid prescriptionId, CancellationToken ct = default)
    {
        var rxRows = await PrescriptionGraphLoader.LoadAsync(
            _db, @"""Id"" = @prescriptionId", new { prescriptionId }, orderByLimit: "LIMIT 1", ct: ct);
        var rx = rxRows.FirstOrDefault()
            ?? throw new NotFoundException("Prescription");
        var patient = await _db.GetByIdAsync<Patient>(rx.PatientId, ct: ct)
            ?? throw new NotFoundException("Patient");
        var hospitalWhere = SqlFragments.WhereActive<HospitalProfile>(ignoreTenant: false);
        var hospital = await _db.QueryFirstOrDefaultAsync<HospitalProfile>(
            $"""SELECT * FROM "HospitalProfiles" WHERE {hospitalWhere} LIMIT 1""", ct: ct);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Prescription</title>");
        sb.Append("<style>body{font-family:Georgia,serif;max-width:720px;margin:24px auto;color:#111}");
        sb.Append("h1{font-size:20px;margin:0} .muted{color:#666;font-size:13px} table{width:100%;border-collapse:collapse;margin-top:16px}");
        sb.Append("th,td{border:1px solid #ccc;padding:6px 8px;font-size:13px;text-align:left} th{background:#f5f5f5}");
        sb.Append("@media print{body{margin:0}}</style></head><body>");
        sb.Append($"<h1>{hospital?.Name ?? "Hospital"}</h1>");
        if (!string.IsNullOrEmpty(hospital?.Address))
            sb.Append($"<div class='muted'>{hospital.Address}</div>");
        sb.Append($"<hr/><p><strong>Patient:</strong> {patient.Name} · {patient.Phone}");
        if (patient.Age.HasValue) sb.Append($" · {patient.Age}y");
        sb.Append("</p>");
        sb.Append($"<p class='muted'>Date: {rx.PrescribedAt:dd MMM yyyy} · Dr. {rx.DoctorName}</p>");
        if (!string.IsNullOrEmpty(rx.Diagnosis))
            sb.Append($"<p><strong>Diagnosis:</strong> {System.Net.WebUtility.HtmlEncode(rx.Diagnosis)}</p>");
        if (!string.IsNullOrEmpty(rx.ChiefComplaint))
            sb.Append($"<p><strong>Chief complaint:</strong> {System.Net.WebUtility.HtmlEncode(rx.ChiefComplaint)}</p>");
        sb.Append("<table><tr><th>#</th><th>Medicine</th><th>Dosage</th><th>Duration</th><th>Instructions</th></tr>");
        var i = 1;
        foreach (var item in rx.Items)
        {
            sb.Append($"<tr><td>{i++}</td><td>{System.Net.WebUtility.HtmlEncode(item.DrugName)}");
            if (!string.IsNullOrEmpty(item.Strength)) sb.Append($" ({item.Strength})");
            sb.Append($"</td><td>{System.Net.WebUtility.HtmlEncode(item.Dosage ?? item.Frequency ?? "—")}</td>");
            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.Duration ?? "—")}</td>");
            sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(item.PatientInstructions ?? item.Timing ?? "—")}</td></tr>");
        }
        sb.Append("</table>");
        if (rx.Injections.Any())
        {
            sb.Append("<p><strong>Injections:</strong></p><ul>");
            foreach (var j in rx.Injections)
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(j.Name)} — {j.AdministeredAt:dd MMM yyyy}</li>");
            sb.Append("</ul>");
        }
        if (!string.IsNullOrEmpty(rx.FollowUpAdvice))
            sb.Append($"<p><strong>Follow-up:</strong> {System.Net.WebUtility.HtmlEncode(rx.FollowUpAdvice)}</p>");
        sb.Append("<p class='muted' style='margin-top:32px'>This is a computer-generated prescription.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public async Task<LabReportUploadResultDto> UploadLabReportAsync(
        Guid patientId, Guid? visitId, string testName, Stream file, string fileName, string? contentType, long fileSize,
        CancellationToken ct = default)
    {
        if (fileSize <= 0)
            throw new DomainException("File is required", 400);
        if (fileSize > MaxLabReportBytes)
            throw new DomainException("File size exceeds 10 MB limit", 400);

        var ext = Path.GetExtension(Path.GetFileName(fileName));
        if (string.IsNullOrEmpty(ext) || !AllowedLabReportExtensions.Contains(ext))
            throw new DomainException("Allowed file types: PDF, JPEG, PNG", 400);

        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedLabReportContentTypes.Contains(contentType))
            throw new DomainException("Allowed file types: PDF, JPEG, PNG", 400);

        if (await _db.GetByIdAsync<Patient>(patientId, ct: ct) is null)
            throw new NotFoundException("Patient");

        if (visitId.HasValue)
        {
            var visit = await _db.QueryFirstOrDefaultAsync<Visit>(
                """SELECT * FROM "Visits" WHERE "Id" = @visitId AND "PatientId" = @patientId LIMIT 1""",
                new { visitId = visitId.Value, patientId }, ct: ct);
            if (visit is null)
                throw new NotFoundException("Visit");
        }

        var safeName = Path.GetFileName(fileName);
        var storedName = $"{Guid.NewGuid():N}{ext}";
        var relativeDir = _storage.LabReportsPath.Trim('/');
        var absoluteDir = Path.Combine(Directory.GetCurrentDirectory(), relativeDir, patientId.ToString("N"));
        Directory.CreateDirectory(absoluteDir);
        var absolutePath = Path.Combine(absoluteDir, storedName);
        await using (var fs = File.Create(absolutePath))
            await file.CopyToAsync(fs, ct);

        var fileUrl = $"/{relativeDir}/{patientId:N}/{storedName}";
        var report = new LabReport
        {
            PatientId = patientId,
            VisitId = visitId,
            TestName = testName,
            FileUrl = fileUrl,
            ReportedAt = DateTime.UtcNow,
            OrderedByUserId = _tenant.UserId,
        };
        await _db.InsertAsync(report, ct: ct);
        return new LabReportUploadResultDto(report.Id, fileUrl, testName);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadLabReportAsync(Guid id, CancellationToken ct = default)
    {
        var report = await _db.GetByIdAsync<LabReport>(id, ct: ct)
            ?? throw new NotFoundException("Lab report");

        if (string.IsNullOrWhiteSpace(report.FileUrl))
            throw new NotFoundException("Lab report file");

        var relativePath = report.FileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (!File.Exists(absolutePath))
            throw new NotFoundException("Lab report file");

        var ext = Path.GetExtension(absolutePath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream",
        };
        var downloadName = $"{SanitizeFileName(report.TestName)}{ext}";
        var stream = File.OpenRead(absolutePath);
        return (stream, contentType, downloadName);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
    }

    private static string VisitDateKey(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd");

    private static PatientDocumentDto MapDocument(PatientDocument d) => new(
        d.Id, d.PatientId, d.VisitId, d.AppointmentId, d.DocumentType, d.Title,
        d.OriginalFileName, d.ContentType, d.FileSizeBytes, d.FileUrl,
        d.UploadedByName, d.CapturedAt, d.CreatedAt);

    private sealed class VisitChartCardBuilder
    {
        public Guid? VisitId { get; set; }
        public DateTime VisitDate { get; set; }
        public string VisitDateKey { get; set; } = "";
        public string? DoctorName { get; set; }
        public string? Department { get; set; }
        public string? ChiefComplaint { get; set; }
        public string? Diagnosis { get; set; }
        public string? DoctorNotes { get; set; }
        public string Status { get; set; } = "";
        public List<VitalSignsDto> Vitals { get; } = new();
        public List<ClinicalNoteDto> Notes { get; } = new();
        public List<PrescriptionDto> Prescriptions { get; } = new();
        public List<PatientDocumentDto> PaperNotes { get; } = new();

        public VisitChartCardDto ToDto() => new(
            VisitId, VisitDate, VisitDateKey, DoctorName, Department,
            ChiefComplaint, Diagnosis, DoctorNotes, Status,
            Vitals, Notes, Prescriptions, PaperNotes);
    }

    private static VitalSignsDto MapVital(VitalSigns v) => new(
        v.Id, v.PatientId, v.VisitId, v.MeasuredAt, v.RecordedByName,
        v.HeightCm, v.WeightKg, v.Bmi,
        v.SystolicBp, v.DiastolicBp, v.HeartRate, v.Temperature,
        v.RespiratoryRate, v.OxygenSaturation,
        v.BloodSugarFasting, v.BloodSugarPostprandial, v.Hba1c, v.Notes);

    private static ClinicalNoteDto MapNote(ClinicalNote n) => new(
        n.Id, n.PatientId, n.VisitId, n.AuthorName, n.CreatedAt,
        n.NoteType, n.Subjective, n.Objective, n.Assessment, n.Plan);

    private static PrescriptionDto MapRx(Prescription rx) => new(
        rx.Id, rx.PatientId, rx.VisitId, rx.DoctorUserId, rx.DoctorName, rx.PrescribedAt,
        rx.Diagnosis, rx.ChiefComplaint, rx.ClinicalNotes, rx.FollowUpAdvice, rx.NextVisitDate,
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
