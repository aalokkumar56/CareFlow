using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class QueueEntry : TenantEntity
{
    public Guid AppointmentId { get; set; }
    public Guid PatientId { get; set; }
    public Guid FacilityId { get; set; }
    public Guid ReservationId { get; set; }
    public Guid OperatingSettingId { get; set; }
    public string Timezone { get; set; } = "";
    public DateOnly ArrivalDate { get; set; }
    public DateTime ArrivedAtUtc { get; set; }
    public long Token { get; set; }
    public long AppointmentRevision { get; set; }
}
public sealed class QueueCounter : TenantEntity
{
    public Guid FacilityId { get; set; }
    public DateOnly ArrivalDate { get; set; }
    public long LastToken { get; set; }
}
