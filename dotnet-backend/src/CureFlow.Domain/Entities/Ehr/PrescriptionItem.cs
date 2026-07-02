using CureFlow.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>An individual drug line on a prescription with full transparency.</summary>
public class PrescriptionItem : TenantEntity
{
    public Guid PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public string DrugName { get; set; } = string.Empty;
    public string? GenericName { get; set; }
    public string? Strength { get; set; }              // "500mg"
    public string? Form { get; set; }                  // "tablet" | "syrup" | "capsule"
    public Enums.PrescriptionRouteType Route { get; set; } = Enums.PrescriptionRouteType.Oral;
    public string? Dosage { get; set; }                // "1-0-1"
    public string? Frequency { get; set; }             // "twice a day"
    public string? Duration { get; set; }              // "7 days"
    public string? Timing { get; set; }                // "after food"
    public int? Quantity { get; set; }
    /// <summary>WHY this drug — transparency requirement.</summary>
    public string ReasonForPrescribing { get; set; } = string.Empty;
    public string? PossibleSideEffects { get; set; }
    public string? PatientInstructions { get; set; }
    public bool IsContinuation { get; set; }            // continuing prior med
    public bool IsAcute { get; set; } = true;           // acute vs chronic
}
