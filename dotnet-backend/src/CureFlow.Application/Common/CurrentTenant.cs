
namespace CureFlow.Application.Common;

public class CurrentTenant : ITenantContext
{
    public Guid TenantId { get; set; } = Guid.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool IsPlatformUser { get; set; }
    public IReadOnlyList<string> CurrentPermissions { get; set; } = Array.Empty<string>();
    public Guid? PlatformSessionId { get; set; }
    public int PlatformSessionRevision { get; set; }
    public string? ServiceIdentity { get; set; }
}

