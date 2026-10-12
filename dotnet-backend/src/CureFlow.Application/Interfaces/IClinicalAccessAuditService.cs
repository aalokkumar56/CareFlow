namespace CureFlow.Application.Interfaces;

public record ClinicalAccessEvidence(Guid? PatientId, string ResourceType, Guid? ResourceId,
    string Action, string Outcome, string? ResourceVersion = null, string Purpose = "care");
public interface IClinicalAccessAuditService
{
    Task RecordAsync(ClinicalAccessEvidence evidence, CancellationToken ct, ICureFlowDbSession? transaction = null);
}
