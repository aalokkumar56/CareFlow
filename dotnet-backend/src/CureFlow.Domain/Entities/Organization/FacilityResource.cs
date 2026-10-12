using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Organization;
public sealed class FacilityResource : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid LocationId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string ResourceType { get; set; } = "room";
    public int Capacity { get; set; } = 1;
    public string OperationalStatus { get; set; } = "disabled";
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveUntil { get; set; }
    public long Revision { get; set; } = 1;
}