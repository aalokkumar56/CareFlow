using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Organization;

/// <summary>Immutable administrative settings version; never changes clinical UTC instants or date-only identity values.</summary>
public sealed class FacilityOperatingSetting : TenantEntity
{
    public Guid FacilityId { get; set; }
    public long Revision { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public string Timezone { get; set; } = "";
    public string Language { get; set; } = "";
    public string DateFormat { get; set; } = "";
    public string TimeFormat { get; set; } = "";
    public string Reason { get; set; } = "";
}
