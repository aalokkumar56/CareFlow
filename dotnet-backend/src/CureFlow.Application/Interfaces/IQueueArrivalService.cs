namespace CureFlow.Application.Interfaces;
public record QueueArrivalView(Guid Id, Guid AppointmentId, Guid PatientId, Guid FacilityId,
    DateOnly ArrivalDate, long Token, DateTime ArrivedAtUtc, string Timezone, long AppointmentRevision);
public interface IQueueArrivalService
{
    Task<IdempotentCommandResult> CheckInAsync(Guid facilityId, Guid appointmentId, long expectedRevision,
        string key, CancellationToken ct);
    Task<QueueArrivalView> GetAsync(Guid facilityId, Guid appointmentId, CancellationToken ct);
    Task<IReadOnlyList<QueueArrivalView>> ListAsync(Guid facilityId, DateOnly date, long? afterToken, int take, CancellationToken ct);
}
