using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Ehr;

namespace CureFlow.Infrastructure.Storage;

public record LegacyDocumentMigrationResult(Guid Id, string Kind, string Status, string? Sha256);

// Explicit operator invocation only; not a hosted service/controller. Runs in an
// established tenant scope. Returns a resumable manifest; never deletes originals.
public sealed class PrivateDocumentMigrationService(ICureFlowDbSession db, IPrivateDocumentStorage objects)
{
    public async Task<IReadOnlyList<LegacyDocumentMigrationResult>> MigrateTenantAsync(CancellationToken ct)
    {
        var results = new List<LegacyDocumentMigrationResult>();
        foreach (var doc in await db.GetAllAsync<PatientDocument>(ct: ct))
        {
            if (doc.StorageKey is not null) { results.Add(new(doc.Id, "patient_document", "AlreadyMapped", doc.Sha256)); continue; }
            try
            {
                await using var source = await objects.OpenLegacyReadAsync(doc.PatientId, doc.FileUrl, false, ct);
                var stored = await objects.StageAsync(db.TenantId, source, doc.ContentType, source.Length, 10 * 1024 * 1024, ct);
                await objects.PublishAsync(db.TenantId, stored, ct);
                await db.TransactionAsync(async store =>
                {
                    var current = await store.QuerySingleAsync<PatientDocument>("SELECT * FROM \"PatientDocuments\" WHERE \"Id\"=@Id FOR UPDATE", new { doc.Id }, ct: ct);
                    if (current.StorageKey is not null) return;
                    if (current.FileUrl != doc.FileUrl || current.PatientId != doc.PatientId) throw new InvalidOperationException("Legacy mapping changed during import.");
                    await AddVersion(store, stored, doc.Id, "patient_document", doc.FileUrl, ct);
                    current.StorageKey = stored.Key; current.StorageVersion = stored.Version; current.Sha256 = stored.Sha256; current.StoredLength = stored.Length;
                    await store.UpdateAsync(current, ct: ct);
                }, ct);
                results.Add(new(doc.Id, "patient_document", "MappedOriginalRetained", stored.Sha256));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { results.Add(new(doc.Id, "patient_document", "Unresolved:" + error.GetType().Name, null)); }
        }
        foreach (var report in await db.GetAllAsync<LabReport>(ct: ct))
        {
            if (string.IsNullOrWhiteSpace(report.FileUrl)) continue;
            if (report.StorageKey is not null) { results.Add(new(report.Id, "lab_report", "AlreadyMapped", report.Sha256)); continue; }
            try
            {
                await using var source = await objects.OpenLegacyReadAsync(report.PatientId, report.FileUrl, true, ct);
                var ext = Path.GetExtension(report.FileUrl).ToLowerInvariant();
                var type = ext switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "application/octet-stream" };
                var stored = await objects.StageAsync(db.TenantId, source, type, source.Length, 10 * 1024 * 1024, ct);
                await objects.PublishAsync(db.TenantId, stored, ct);
                await db.TransactionAsync(async store =>
                {
                    var current = await store.QuerySingleAsync<LabReport>("SELECT * FROM \"LabReports\" WHERE \"Id\"=@Id FOR UPDATE", new { report.Id }, ct: ct);
                    if (current.StorageKey is not null) return;
                    if (current.FileUrl != report.FileUrl || current.PatientId != report.PatientId) throw new InvalidOperationException("Legacy mapping changed during import.");
                    await AddVersion(store, stored, report.Id, "lab_report", report.FileUrl!, ct);
                    current.StorageKey = stored.Key; current.StorageVersion = stored.Version; current.Sha256 = stored.Sha256; current.StoredLength = stored.Length;
                    current.StoredContentType = type; current.OriginalFileName = Path.GetFileName(report.FileUrl);
                    await store.UpdateAsync(current, ct: ct);
                }, ct);
                results.Add(new(report.Id, "lab_report", "MappedOriginalRetained", stored.Sha256));
            }
            catch (Exception error) when (error is not OperationCanceledException)
            { results.Add(new(report.Id, "lab_report", "Unresolved:" + error.GetType().Name, null)); }
        }
        return results;
    }
    private static Task AddVersion(ICureFlowDbSession store, PrivateDocumentObject item, Guid id, string kind, string legacyUrl, CancellationToken ct) =>
        store.InsertAsync(new PrivateDocumentVersion { Id = item.Version, DocumentId = id, DocumentKind = kind, ObjectKey = item.Key,
            Sha256 = item.Sha256, Length = item.Length, ContentType = item.ContentType, LegacyFileUrl = legacyUrl }, ct: ct);
}
