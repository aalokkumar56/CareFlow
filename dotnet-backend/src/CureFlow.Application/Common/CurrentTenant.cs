
namespace CureFlow.Application.Common;

public class CurrentTenant : ITenantContext
{
    public Guid TenantId { get; set; } = Guid.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserRole { get; set; }
    public bool IsAuthenticated { get; set; }
}
