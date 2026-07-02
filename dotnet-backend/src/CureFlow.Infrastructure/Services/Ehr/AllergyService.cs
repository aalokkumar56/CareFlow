using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Ehr;
using CureFlow.Infrastructure.Persistence.Dapper;

namespace CureFlow.Infrastructure.Services.Ehr;

public class AllergyService : IAllergyService
{
    private readonly ICureFlowDbSession _db;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public AllergyService(ICureFlowDbSession db, ITenantContext tenant, IAuditService audit)
    {
        _db = db;
        _tenant = tenant;
        _audit = audit;
    }

    public async Task<Guid> AddAsync(CreateAllergyRequest req, CancellationToken ct = default)
    {
        var a = new Allergy
        {
            PatientId = req.PatientId,
            Type = req.Type,
            Allergen = req.Allergen,
            Severity = req.Severity,
            Reaction = req.Reaction,
            FirstObserved = DateTimeHelper.EnsureUtc(req.FirstObserved),
            Notes = req.Notes,
            RecordedByUserId = _tenant.UserId ?? Guid.Empty,
            RecordedByName = _tenant.UserEmail,
        };
        await _db.InsertAsync(a, ct: ct);
        await _audit.LogAsync("allergy.add", "allergy", a.Id.ToString(), new { req.Allergen }, ct);
        return a.Id;
    }

    public async Task<IReadOnlyList<AllergyDto>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        var rows = await _db.QueryAsync<Allergy>(
            """
            SELECT * FROM "Allergies"
            WHERE "PatientId" = @patientId AND "IsDeleted" = false AND "TenantId" = @TenantId
            ORDER BY "CreatedAt" DESC
            """,
            new { patientId },
            ct: ct);
        return rows.Select(a => new AllergyDto(a.Id, a.PatientId, a.Type, a.Allergen, a.Severity,
            a.Reaction, a.FirstObserved, a.Notes, a.RecordedByName, a.CreatedAt)).ToList();
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var a = await _db.GetByIdAsync<Allergy>(id, ct: ct)
            ?? throw new NotFoundException("Allergy");
        a.IsDeleted = true;
        await _db.UpdateAsync(a, ct: ct);
        await _audit.LogAsync("allergy.remove", "allergy", id.ToString(), null, ct);
    }
}
