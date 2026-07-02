using CureFlow.Application.DTOs;
using FluentValidation;

namespace CureFlow.Application.Validation;

public class UpdatePatientValidator : AbstractValidator<UpdatePatientRequest>
{
    public UpdatePatientValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).When(x => x.Name != null);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20).When(x => x.Phone != null);
        RuleFor(x => x.Age).InclusiveBetween(0, 150).When(x => x.Age.HasValue);
    }
}
