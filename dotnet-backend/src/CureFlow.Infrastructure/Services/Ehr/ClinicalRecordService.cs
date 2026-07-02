using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Ehr;

public class ClinicalRecordService : IClinicalRecordService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;

    public ClinicalRecordService(ICureFlowDbSession db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<Guid> AddVitalsAsync(CreateVitalSignsRequest r, CancellationToken ct = default)
    {
        EnsureTenant();
        EnsurePatientId(r.PatientId);
        var v = new VitalSigns
        {
            PatientId = r.PatientId,
            AppointmentId = r.AppointmentId,
            VisitId = r.VisitId,
            MeasuredAt = DateTimeHelper.EnsureUtc(r.MeasuredAt) ?? DateTime.UtcNow,
            HeightCm = r.HeightCm,
            WeightKg = r.WeightKg,
            SystolicBp = ToWholeNumber(r.SystolicBp),
            DiastolicBp = ToWholeNumber(r.DiastolicBp),
            HeartRate = ToWholeNumber(r.HeartRate),
            Temperature = r.Temperature,
            RespiratoryRate = ToWholeNumber(r.RespiratoryRate),
            OxygenSaturation = ToWholeNumber(r.OxygenSaturation),
            BloodSugarFasting = r.BloodSugarFasting,
            BloodSugarPostprandial = r.BloodSugarPostprandial,
            Hba1c = r.Hba1c,
            Notes = r.Notes,
            RecordedByUserId = _tenant.UserId ?? Guid.Empty,
            RecordedByName = _tenant.UserEmail,
        };
        if (r.HeightCm.HasValue && r.WeightKg.HasValue && r.HeightCm.Value > 0)
            v.Bmi = Math.Round(r.WeightKg.Value / (decimal)Math.Pow((double)r.HeightCm.Value / 100, 2), 1);
        await _db.InsertAsync(v, ct: ct);
        return v.Id;
    }

    public async Task<Guid> AddNoteAsync(CreateClinicalNoteRequest r, CancellationToken ct = default)
    {
        EnsureTenant();
        EnsurePatientId(r.PatientId);
        var user = await _db.GetByIdAsync<User>(_tenant.UserId ?? Guid.Empty, ct: ct);
        var n = new ClinicalNote
        {
            PatientId = r.PatientId,
            AppointmentId = r.AppointmentId,
            VisitId = r.VisitId,
            AuthorUserId = _tenant.UserId ?? Guid.Empty,
            AuthorName = user?.Name ?? "User",
            NoteType = r.NoteType ?? "progress",
            Subjective = r.Subjective ?? "",
            Objective = r.Objective ?? "",
            Assessment = r.Assessment ?? "",
            Plan = r.Plan ?? "",
        };
        await _db.InsertAsync(n, ct: ct);
        return n.Id;
    }

    public async Task UpdateNoteAsync(Guid id, UpdateClinicalNoteRequest r, CancellationToken ct = default)
    {
        EnsureTenant();
        var n = await _db.GetByIdAsync<ClinicalNote>(id, ct: ct)
            ?? throw new NotFoundException("Clinical note");
        if (r.NoteType != null) n.NoteType = r.NoteType;
        if (r.Subjective != null) n.Subjective = r.Subjective;
        if (r.Objective != null) n.Objective = r.Objective;
        if (r.Assessment != null) n.Assessment = r.Assessment;
        if (r.Plan != null) n.Plan = r.Plan;
        await _db.UpdateAsync(n, ct: ct);
    }

    public async Task<Guid> AddMedicalHistoryAsync(CreateMedicalHistoryRequest r, CancellationToken ct = default)
    {
        EnsureTenant();
        EnsurePatientId(r.PatientId);
        var m = new MedicalHistoryItem
        {
            PatientId = r.PatientId,
            Category = r.Category,
            Title = r.Title,
            OnsetDate = DateTimeHelper.EnsureUtc(r.OnsetDate),
            IsOngoing = r.IsOngoing,
            Description = r.Description,
        };
        await _db.InsertAsync(m, ct: ct);
        return m.Id;
    }

    public async Task<Guid> AddFamilyHistoryAsync(CreateFamilyHistoryRequest r, CancellationToken ct = default)
    {
        EnsureTenant();
        EnsurePatientId(r.PatientId);
        var f = new FamilyHistoryItem
        {
            PatientId = r.PatientId,
            Relation = r.Relation,
            Condition = r.Condition,
            AgeOfOnset = r.AgeOfOnset,
            Notes = r.Notes,
        };
        await _db.InsertAsync(f, ct: ct);
        return f.Id;
    }

    public async Task<IReadOnlyList<VitalSignsDto>> ListVitalsAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<VitalSigns>(
            """
            SELECT * FROM "VitalSigns"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            ORDER BY "MeasuredAt" DESC
            """,
            new { patientId },
            ct: ct);
        return rows.Select(v => new VitalSignsDto(
            v.Id, v.PatientId, v.VisitId, v.MeasuredAt, v.RecordedByName,
            v.HeightCm, v.WeightKg, v.Bmi,
            v.SystolicBp, v.DiastolicBp, v.HeartRate, v.Temperature,
            v.RespiratoryRate, v.OxygenSaturation,
            v.BloodSugarFasting, v.BloodSugarPostprandial, v.Hba1c, v.Notes)).ToList();
    }

    public async Task<IReadOnlyList<ClinicalNoteDto>> ListNotesAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<ClinicalNote>(
            """
            SELECT * FROM "ClinicalNotes"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            """,
            new { patientId },
            ct: ct);
        return rows.Select(n => new ClinicalNoteDto(
            n.Id, n.PatientId, n.VisitId, n.AuthorName, n.CreatedAt,
            n.NoteType, n.Subjective, n.Objective, n.Assessment, n.Plan)).ToList();
    }

    public async Task<IReadOnlyList<MedicalHistoryDto>> ListMedicalHistoryAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<MedicalHistoryItem>(
            """
            SELECT * FROM "MedicalHistory"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            """,
            new { patientId },
            ct: ct);
        return rows.Select(m => new MedicalHistoryDto(
            m.Id, m.PatientId, m.Category, m.Title,
            m.OnsetDate, m.IsOngoing, m.Description, m.CreatedAt)).ToList();
    }

    public async Task<IReadOnlyList<FamilyHistoryDto>> ListFamilyHistoryAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<FamilyHistoryItem>(
            """
            SELECT * FROM "FamilyHistory"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            """,
            new { patientId },
            ct: ct);
        return rows.Select(f => new FamilyHistoryDto(
            f.Id, f.PatientId, f.Relation, f.Condition,
            f.AgeOfOnset, f.Notes, f.CreatedAt)).ToList();
    }

    private static int? ToWholeNumber(decimal? value) =>
        value.HasValue ? (int)Math.Round(value.Value, MidpointRounding.AwayFromZero) : null;

    private static void EnsurePatientId(Guid patientId)
    {
        if (patientId == Guid.Empty)
            throw new ValidationException("patient_id is required");
    }

    private void EnsureTenant()
    {
        if (_tenant.TenantId == Guid.Empty)
            throw new ValidationException("Authentication required");
    }
}
