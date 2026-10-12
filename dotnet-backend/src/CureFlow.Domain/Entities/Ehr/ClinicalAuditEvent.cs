using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Ehr;

public sealed class ClinicalAuditEvent : TenantEntity
{
    public string ActorKind { get; set; } = string.Empty;
    public Guid? ActorId { get; set; }
    public string? ServiceIdentity { get; set; }
    public Guid? PatientId { get; set; }
    public string ResourceType { get; set; } = string.Empty;
    public Guid? ResourceId { get; set; }
    public string? ResourceVersion { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Purpose { get; set; } = "care";
    public string Outcome { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
