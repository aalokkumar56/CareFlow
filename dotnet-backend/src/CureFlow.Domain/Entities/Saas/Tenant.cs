using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;

namespace CureFlow.Domain.Entities.Saas;

/// <summary>A hospital using CureFlow as SaaS. All other entities scope by TenantId.</summary>
public class Tenant : BaseEntity
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = "Hospital";
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Country { get; set; } = "IN";
    public string? Timezone { get; set; } = "Asia/Kolkata";

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Trial;
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trialing;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime? SubscriptionRenewsAt { get; set; }

    public int SeatLimit { get; set; } = 5;
    public int PatientLimit { get; set; } = 500;
    public int MessagesQuotaMonthly { get; set; } = 1000;
    public ICollection<User> Users { get; set; } = new List<User>();
}
