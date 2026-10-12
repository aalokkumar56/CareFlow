using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Ehr;

public sealed class PrivateDocumentVersion : TenantEntity
{
    public Guid DocumentId { get; set; }
    public string DocumentKind { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Length { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string? LegacyFileUrl { get; set; }
}
