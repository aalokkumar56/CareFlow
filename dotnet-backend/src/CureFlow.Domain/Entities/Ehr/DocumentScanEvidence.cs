using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Ehr;

public sealed class DocumentScanEvidence : TenantEntity
{
    public Guid DocumentId { get; set; }
    public Guid Version { get; set; }
    public string Sha256 { get; set; } = "";
    public string Verdict { get; set; } = "";
    public string Engine { get; set; } = "";
    public string EngineVersion { get; set; } = "";
    public string Code { get; set; } = "";
}
