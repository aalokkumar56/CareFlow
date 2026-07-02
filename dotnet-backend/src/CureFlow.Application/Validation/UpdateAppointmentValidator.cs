using CureFlow.Application.DTOs;
using FluentValidation;

namespace CureFlow.Application.Validation;

public class UpdateAppointmentValidator : AbstractValidator<UpdateAppointmentRequest>
{
    public UpdateAppointmentValidator()
    {
        RuleFor(x => x.ScheduledAt)
            .GreaterThan(DateTime.UtcNow.AddMinutes(-5))
            .When(x => x.ScheduledAt.HasValue)
            .WithMessage("Scheduled time must be in the future");

        RuleFor(x => x.Department)
            .NotEmpty()
            .MaximumLength(200)
            .When(x => x.Department != null);

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(5, 480)
            .When(x => x.DurationMinutes.HasValue)
            .WithMessage("Duration must be between 5 and 480 minutes");

        RuleFor(x => x.ChiefComplaint)
            .MaximumLength(2000)
            .When(x => x.ChiefComplaint != null);

        RuleFor(x => x.Notes)
            .MaximumLength(4000)
            .When(x => x.Notes != null);
    }
}
