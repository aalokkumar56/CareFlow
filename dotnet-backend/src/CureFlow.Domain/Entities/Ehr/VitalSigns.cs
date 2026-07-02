using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Ehr;

/// <summary>Vital signs recorded at a visit.</summary>
public class VitalSigns : TenantEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string? RecordedByName { get; set; }
    public DateTime MeasuredAt { get; set; } = DateTime.UtcNow;
    public decimal? HeightCm { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? Bmi { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    public decimal? Temperature { get; set; }
    public int? RespiratoryRate { get; set; }
    public int? OxygenSaturation { get; set; }
    public decimal? BloodSugarFasting { get; set; }
    public decimal? BloodSugarPostprandial { get; set; }
    public decimal? Hba1c { get; set; }
    public string? Notes { get; set; }
}
