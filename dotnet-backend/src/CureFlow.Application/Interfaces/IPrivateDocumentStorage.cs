namespace CureFlow.Application.Interfaces;

public record PrivateDocumentObject(string Key, Guid Version, string Sha256, long Length, string ContentType);

public record PrivateDocumentInventoryItem(string Key, bool Staged);
public interface IPrivateDocumentStorage
{
    Task<IReadOnlyList<PrivateDocumentInventoryItem>> InventoryAsync(Guid tenantId, CancellationToken ct);
    Task<PrivateDocumentObject> StageAsync(Guid tenantId, Stream source, string contentType, long declaredLength, long maximumLength, CancellationToken ct);
    Task PublishAsync(Guid tenantId, PrivateDocumentObject item, CancellationToken ct);
    Task<Stream> OpenReadAsync(Guid tenantId, string key, string sha256, long length, CancellationToken ct);
    Task<Stream> OpenLegacyReadAsync(Guid patientId, string fileUrl, bool labReport, CancellationToken ct);
}
