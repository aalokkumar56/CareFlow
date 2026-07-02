using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Past medical history items (diabetes, hypertension, surgeries).</summary>
public class MedicalHistoryItem : TenantEntity
{
    public Guid PatientId { get; set; }
    public string Category { get; set; } = "condition";
    public string Title { get; set; } = string.Empty;
    public DateTime? OnsetDate { get; set; }
    public bool IsOngoing { get; set; } = true;
    public string? Description { get; set; }
}
