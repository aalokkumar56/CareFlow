using CureFlow.Domain.Enums;
namespace CureFlow.Application.Options;

public sealed class OrganizationOnboardingDefaults
{
    public bool RequireProfile { get; set; } = true;
    public bool RequireTeam { get; set; } = true;
    public bool RequireWhatsApp { get; set; } = true;
}
public sealed class OrganizationOnboardingOptions
{
    public Dictionary<TenantType, OrganizationOnboardingDefaults> Types { get; set; } = new();
    public OrganizationOnboardingDefaults For(TenantType type)
    {
        if (!Enum.IsDefined(type) || Types.Any(x => !Enum.IsDefined(x.Key) || x.Value is null))
            throw new InvalidOperationException("Organization onboarding types must be existing defined tenant types.");
        return Types.TryGetValue(type, out var defaults) ? defaults : new OrganizationOnboardingDefaults();
    }
}
