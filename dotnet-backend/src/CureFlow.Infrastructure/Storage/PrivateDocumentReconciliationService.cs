using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Ehr;
namespace CureFlow.Infrastructure.Storage;

public record DocumentReconciliationItem(Guid? VersionId, string Key, string Status);

// Dry reconciliation only. Never removes partial uploads, orphan objects or originals.
public sealed class PrivateDocumentReconciliationService(ICureFlowDbSession db, IPrivateDocumentStorage objects)
{
    public async Task<IReadOnlyList<DocumentReconciliationItem>> ReconcileTenantAsync(CancellationToken ct)
    {
        var versions = await db.GetAllAsync<PrivateDocumentVersion>(ct: ct);
        var imports = await db.GetAllAsync<CureFlow.Domain.Entities.PatientImportBatch>(ct: ct);
        var exports = await db.GetAllAsync<CureFlow.Domain.Entities.PatientExportJob>(ct: ct);
        var referenced = versions.Select(x => x.ObjectKey).ToHashSet(StringComparer.Ordinal);
        foreach(var batch in imports) referenced.Add(batch.SourceObjectKey);
        foreach(var job in exports) if(job.ObjectKey is not null) referenced.Add(job.ObjectKey);
        var result = new List<DocumentReconciliationItem>();
        foreach (var item in versions)
        {
            try
            {
                await using var stream = await objects.OpenReadAsync(db.TenantId, item.ObjectKey, item.Sha256, item.Length, ct);
                result.Add(new(item.Id, item.ObjectKey, "Verified"));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { result.Add(new(item.Id, item.ObjectKey, "Unresolved:" + error.GetType().Name)); }
        }
        foreach (var item in await objects.InventoryAsync(db.TenantId, ct))
            if (item.Staged || !referenced.Contains(item.Key))
                result.Add(new(null, item.Key, item.Staged ? "StagedOrAbandonedRetained" : "UnreferencedRetained"));
        return result;
    }
}
