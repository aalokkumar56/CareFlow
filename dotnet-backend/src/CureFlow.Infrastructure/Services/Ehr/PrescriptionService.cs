using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Ehr;

public class PrescriptionService : IPrescriptionService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public PrescriptionService(ICureFlowDbSession db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<Guid> CreateAsync(CreatePrescriptionRequest req, CancellationToken ct = default)
    {
        var doctor = _tenant.UserId.HasValue
            ? await _db.GetByIdAsync<User>(_tenant.UserId.Value, ct: ct)
            : null;
        var rx = new Prescription
        {
            PatientId = req.PatientId,
            AppointmentId = req.AppointmentId,
            VisitId = req.VisitId,
            DoctorUserId = _tenant.UserId ?? Guid.Empty,
            DoctorName = doctor?.Name ?? "Doctor",
            PrescribedAt = DateTime.UtcNow,
            Diagnosis = req.Diagnosis,
            ChiefComplaint = req.ChiefComplaint,
            ClinicalNotes = req.ClinicalNotes,
            FollowUpAdvice = req.FollowUpAdvice,
            NextVisitDate = DateTimeHelper.EnsureUtc(req.NextVisitDate),
            Items = req.Items.Select(i => new PrescriptionItem
            {
                DrugName = i.DrugName,
                GenericName = i.GenericName,
                Strength = i.Strength,
                Form = i.Form,
                Route = i.Route,
                Dosage = i.Dosage,
                Frequency = i.Frequency,
                Duration = i.Duration,
                Timing = i.Timing,
                Quantity = i.Quantity,
                ReasonForPrescribing = i.ReasonForPrescribing,
                PossibleSideEffects = i.PossibleSideEffects,
                PatientInstructions = i.PatientInstructions,
                IsContinuation = i.IsContinuation,
                IsAcute = i.IsAcute,
            }).ToList(),
            Injections = req.Injections?.Select(j => new Injection
            {
                Name = j.Name,
                GenericName = j.GenericName,
                Strength = j.Strength,
                Site = j.Site,
                Route = j.Route,
                AdministeredAt = DateTimeHelper.EnsureUtc(j.AdministeredAt),
                AdministeredByUserId = j.AdministeredByUserId ?? _tenant.UserId,
                AdministeredBy = j.AdministeredBy,
                BatchNumber = j.BatchNumber,
                ExpiryDate = DateTimeHelper.EnsureUtc(j.ExpiryDate),
                ReasonForInjection = j.ReasonForInjection,
                AdverseReaction = j.AdverseReaction,
                Notes = j.Notes,
            }).ToList() ?? new List<Injection>(),
        };

        await _db.TransactionAsync(async session =>
        {
            await session.InsertAsync(rx, ct: ct);
            foreach (var item in rx.Items)
            {
                item.PrescriptionId = rx.Id;
                await session.InsertAsync(item, ct: ct);
            }
            foreach (var injection in rx.Injections)
            {
                injection.PrescriptionId = rx.Id;
                await session.InsertAsync(injection, ct: ct);
            }
        }, ct);

        await _audit.LogAsync("prescription.create", "prescription", rx.Id.ToString(),
            new { items = req.Items.Count, injections = req.Injections?.Count ?? 0 }, ct);
        return rx.Id;
    }

    public async Task<PrescriptionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var rows = await LoadPrescriptionGraphAsync(id: id, ct: ct);
        var rx = rows.FirstOrDefault() ?? throw new NotFoundException("Prescription");
        return Map(rx);
    }

    public async Task<IReadOnlyList<PrescriptionDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await LoadPrescriptionGraphAsync(patientId: patientId, ct: ct);
        return rows.Select(Map).ToList();
    }

    private async Task<IReadOnlyList<Prescription>> LoadPrescriptionGraphAsync(
        Guid? patientId = null, Guid? id = null, CancellationToken ct = default)
    {
        string sql;
        object param;
        if (id.HasValue)
        {
            sql = """
                SELECT * FROM "Prescriptions"
                WHERE "Id" = @id AND "IsDeleted" = false AND "TenantId" = @TenantId
                """;
            param = new { id = id.Value };
        }
        else
        {
            sql = """
                SELECT * FROM "Prescriptions"
                WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
                ORDER BY "PrescribedAt" DESC
                """;
            param = new { patientId = patientId!.Value };
        }

        var prescriptions = (await _db.QueryAsync<Prescription>(sql, param, ct: ct)).ToList();
        if (prescriptions.Count == 0)
            return prescriptions;

        var ids = prescriptions.Select(p => p.Id).ToArray();
        var items = await _db.QueryAsync<PrescriptionItem>(
            """
            SELECT * FROM "PrescriptionItems"
            WHERE "PrescriptionId" = ANY(@ids) AND "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            new { ids },
            ct: ct);
        var injections = await _db.QueryAsync<Injection>(
            """
            SELECT * FROM "Injections"
            WHERE "PrescriptionId" = ANY(@ids) AND "IsDeleted" = false AND "TenantId" = @TenantId
            """,
            new { ids },
            ct: ct);

        var itemsByRx = items.GroupBy(i => i.PrescriptionId).ToDictionary(g => g.Key, g => g.ToList());
        var injectionsByRx = injections.GroupBy(i => i.PrescriptionId).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var rx in prescriptions)
        {
            rx.Items = itemsByRx.TryGetValue(rx.Id, out var rxItems) ? rxItems : new List<PrescriptionItem>();
            rx.Injections = injectionsByRx.TryGetValue(rx.Id, out var rxInj) ? rxInj : new List<Injection>();
        }

        return prescriptions;
    }

    private static PrescriptionDto Map(Prescription rx) => new(
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
