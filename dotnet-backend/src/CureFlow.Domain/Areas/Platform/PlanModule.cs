using CureFlow.Domain.Common;
using CureFlow.Domain.Enums;
namespace CureFlow.Domain.Entities.Saas;
public sealed class PlanModule : BaseEntity
{
    public SubscriptionPlan Plan { get; set; }
    public Guid PlatformModuleId { get; set; }
    public bool IsEnabled { get; set; }
}
