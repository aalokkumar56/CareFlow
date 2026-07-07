using CureFlow.Domain.Common;

namespace CureFlow.Domain.Entities.Saas;

public class TenantOnboardingState : TenantEntity
{
    public bool ProfileComplete { get; set; }
    public bool WhatsAppConnected { get; set; }
    public bool TeamInvited { get; set; }
    public DateTime? CompletedAt { get; set; }
}
