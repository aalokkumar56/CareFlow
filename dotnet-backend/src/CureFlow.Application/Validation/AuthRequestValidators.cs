using CureFlow.Application.DTOs;
using FluentValidation;

namespace CureFlow.Application.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MaximumLength(1024);
        RuleFor(x => x.TenantSlug).MaximumLength(200);
    }
}

public sealed class RegisterTenantRequestValidator : AbstractValidator<RegisterTenantRequest>
{
    public RegisterTenantRequestValidator()
    {
        RuleFor(x => x.HospitalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AdminEmail).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.AdminPassword).NotEmpty().MinimumLength(8).MaximumLength(1024);
        RuleFor(x => x.Phone).MaximumLength(20);
        RuleFor(x => x.TenantType).IsInEnum();
    }
}
