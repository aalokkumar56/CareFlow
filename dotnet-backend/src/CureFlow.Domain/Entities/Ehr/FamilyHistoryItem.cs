using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Family history - hereditary conditions.</summary>
public class FamilyHistoryItem : TenantEntity
{
    public Guid PatientId { get; set; }
    public string Relation { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public int? AgeOfOnset { get; set; }
    public string? Notes { get; set; }
}
