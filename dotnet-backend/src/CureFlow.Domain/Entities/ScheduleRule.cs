using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;

/// <summary>Explicit facility-local weekly rule; retained independently of legacy UTC doctor schedules.</summary>
public sealed class ScheduleRule : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid? PractitionerUserId { get; set; }
    public Guid? ResourceId { get; set; }
    public Guid OperatingSettingId { get; set; }
    public string Timezone { get; set; } = "";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveThrough { get; set; }
    public int WeekdayMask { get; set; }
    public int StartMinute { get; set; }
    public int EndMinute { get; set; }
    public string AmbiguousTimePolicy { get; set; } = "reject";
    public long Revision { get; set; } = 1;
}
