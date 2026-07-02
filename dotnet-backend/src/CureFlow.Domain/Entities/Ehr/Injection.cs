using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Injections - separately tracked due to clinical importance.</summary>
public class Injection : TenantEntity
{
    public Guid PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public string Name { get; set; } = string.Empty;   // "Tetanus Toxoid"
    public string? GenericName { get; set; }
    public string? Strength { get; set; }
    public string? Site { get; set; }                  // "Left deltoid"
    public Enums.PrescriptionRouteType Route { get; set; } = Enums.PrescriptionRouteType.Im;
    public DateTime AdministeredAt { get; set; } = DateTime.UtcNow;
    public Guid? AdministeredByUserId { get; set; }
    public string AdministeredBy { get; set; } = string.Empty;
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string ReasonForInjection { get; set; } = string.Empty;
    public string? AdverseReaction { get; set; }
    public string? Notes { get; set; }
}
