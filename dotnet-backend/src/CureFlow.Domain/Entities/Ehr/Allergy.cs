using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Patient allergy record - critical for safe prescribing.</summary>
public class Allergy : TenantEntity
{
    public Guid PatientId { get; set; }
    public AllergyType Type { get; set; }
    public string Allergen { get; set; } = string.Empty;
    public AllergySeverity Severity { get; set; } = AllergySeverity.Mild;
    public string? Reaction { get; set; }
    public DateTime? FirstObserved { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
}
