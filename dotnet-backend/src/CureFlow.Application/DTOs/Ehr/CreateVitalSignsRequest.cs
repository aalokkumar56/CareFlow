using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

/// <summary>Vitals create payload â€” numeric fields accept decimals from JSON; service rounds whole-number vitals.</summary>
public record CreateVitalSignsRequest(
    Guid PatientId, Guid? AppointmentId, Guid? VisitId, DateTime? MeasuredAt,
    decimal? HeightCm, decimal? WeightKg, decimal? SystolicBp, decimal? DiastolicBp,
    decimal? HeartRate, decimal? Temperature, decimal? RespiratoryRate, decimal? OxygenSaturation,
    decimal? BloodSugarFasting, decimal? BloodSugarPostprandial, decimal? Hba1c,
    string? Notes);
