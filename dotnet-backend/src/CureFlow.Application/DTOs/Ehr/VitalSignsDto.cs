using CureFlow.Domain.Enums;

namespace CureFlow.Application.DTOs;

public record VitalSignsDto(
    Guid Id, Guid PatientId, Guid? VisitId, DateTime MeasuredAt, string? RecordedByName,
    decimal? HeightCm, decimal? WeightKg, decimal? Bmi,
    int? SystolicBp, int? DiastolicBp, int? HeartRate, decimal? Temperature,
    int? RespiratoryRate, int? OxygenSaturation,
    decimal? BloodSugarFasting, decimal? BloodSugarPostprandial, decimal? Hba1c,
    string? Notes);
