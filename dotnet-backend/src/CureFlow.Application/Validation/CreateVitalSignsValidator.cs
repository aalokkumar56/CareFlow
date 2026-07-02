using CureFlow.Application.DTOs;
using FluentValidation;

namespace CureFlow.Application.Validation;

public class CreateVitalSignsValidator : AbstractValidator<CreateVitalSignsRequest>
{
    public CreateVitalSignsValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty().WithMessage("patient_id is required");

        RuleFor(x => x).Must(HasAtLeastOneMeasurement)
            .WithMessage("Enter at least one vital measurement");

        RuleFor(x => x.HeightCm).InclusiveBetween(30, 300)
            .When(x => x.HeightCm.HasValue);
        RuleFor(x => x.WeightKg).InclusiveBetween(0.5m, 500)
            .When(x => x.WeightKg.HasValue);
        RuleFor(x => x.SystolicBp).InclusiveBetween(50, 300)
            .When(x => x.SystolicBp.HasValue);
        RuleFor(x => x.DiastolicBp).InclusiveBetween(30, 200)
            .When(x => x.DiastolicBp.HasValue);
        RuleFor(x => x.HeartRate).InclusiveBetween(20, 300)
            .When(x => x.HeartRate.HasValue);
        RuleFor(x => x.Temperature).InclusiveBetween(90, 115)
            .When(x => x.Temperature.HasValue);
        RuleFor(x => x.RespiratoryRate).InclusiveBetween(5, 80)
            .When(x => x.RespiratoryRate.HasValue);
        RuleFor(x => x.OxygenSaturation).InclusiveBetween(50, 100)
            .When(x => x.OxygenSaturation.HasValue);
    }

    private static bool HasAtLeastOneMeasurement(CreateVitalSignsRequest r) =>
        r.HeightCm.HasValue || r.WeightKg.HasValue || r.SystolicBp.HasValue || r.DiastolicBp.HasValue
        || r.HeartRate.HasValue || r.Temperature.HasValue || r.RespiratoryRate.HasValue
        || r.OxygenSaturation.HasValue || r.BloodSugarFasting.HasValue || r.BloodSugarPostprandial.HasValue
        || r.Hba1c.HasValue || !string.IsNullOrWhiteSpace(r.Notes);
}
