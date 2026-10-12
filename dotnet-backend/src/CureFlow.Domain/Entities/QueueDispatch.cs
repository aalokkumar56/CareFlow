using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class QueuePriorityOverride : TenantEntity
{
    public Guid QueueEntryId { get; set; }
    public Guid PatientId { get; set; }
    public Guid FacilityId { get; set; }
    public DateOnly ArrivalDate { get; set; }
    public long Revision { get; set; }
    public int SortRank { get; set; }
    public string Reason { get; set; } = "";
    public string PolicyVersion { get; set; } = "";
}
public sealed class QueueStart : TenantEntity
{
    public Guid QueueEntryId { get; set; }
    public Guid PatientId { get; set; }
    public Guid FacilityId { get; set; }
    public DateOnly ArrivalDate { get; set; }
    public long Revision { get; set; }
    public int SortRank { get; set; }
    public int DefaultRank { get; set; }
    public string PolicyVersion { get; set; } = "";
}
