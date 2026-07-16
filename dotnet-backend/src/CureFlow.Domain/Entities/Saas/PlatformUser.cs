using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

/// <summary>CureFlow platform operator — not scoped to a hospital tenant.</summary>
public class PlatformUser : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
