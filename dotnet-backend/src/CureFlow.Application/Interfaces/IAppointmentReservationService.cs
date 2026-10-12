namespace CureFlow.Application.Interfaces;
public record AppointmentHoldInput(Guid FacilityId,Guid PractitionerUserId,IReadOnlyList<Guid> ResourceIds,DateTime StartsAtUtc,DateTime EndsAtUtc);
public record AppointmentHoldView(Guid Id,Guid FacilityId,Guid PractitionerUserId,DateTime StartsAtUtc,DateTime EndsAtUtc,
    DateTime HoldExpiresAtUtc,string State,long Revision,IReadOnlyList<Guid> ResourceIds);
public interface IAppointmentReservationService
{
    Task<IdempotentCommandResult> HoldAsync(AppointmentHoldInput input,string key,CancellationToken ct);
    Task<AppointmentHoldView> GetAsync(Guid id,CancellationToken ct);
    Task<IdempotentCommandResult> ReleaseAsync(Guid id,long expectedRevision,string key,CancellationToken ct);
    // Only the booking command may call this after validating/creating its patient appointment in the same transaction.
    Task ConfirmInTransactionAsync(ICureFlowDbSession store,Guid id,long expectedRevision,Guid appointmentId,CancellationToken ct);
}