using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities;

/// <summary>Staff/Doctor user accounts of a hospital. Tied to a single tenant.</summary>
public class User : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;

    // Doctor-specific (when Role == Doctor)
    public string? Specialty { get; set; }
    public string? Qualifications { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Phone { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
