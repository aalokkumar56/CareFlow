using CureFlow.Application.DTOs;
using FluentValidation;

namespace CureFlow.Application.Validation;

public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DoctorUserId).NotEmpty()
            .WithMessage("Select a doctor from Hospital Staff");
        RuleFor(x => x.Department).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ScheduledAt).GreaterThan(DateTime.UtcNow.AddMinutes(-5))
            .WithMessage("Scheduled time must be in the future");
    }
}
