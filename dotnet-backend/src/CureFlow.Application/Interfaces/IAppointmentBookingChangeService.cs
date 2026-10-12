namespace CureFlow.Application.Interfaces;
public record AppointmentRescheduleInput(Guid ReservationId,long ExpectedReservationRevision,string Reason);
public record AppointmentCancelInput(string Reason);
public record AppointmentBookingChangeView(Guid Id,long Revision,string Action,Guid PreviousReservationId,Guid? ReplacementReservationId,
    DateTime PreviousStartsAtUtc,DateTime PreviousEndsAtUtc,DateTime? ReplacementStartsAtUtc,DateTime? ReplacementEndsAtUtc,
    string Reason,DateTime RecordedAtUtc,Guid? RecordedBy);
public interface IAppointmentBookingChangeService
{
    Task<IReadOnlyList<AppointmentBookingChangeView>> HistoryAsync(Guid id,long afterRevision,int limit,CancellationToken ct);
    Task<IdempotentCommandResult> RescheduleAsync(Guid id,long expectedRevision,AppointmentRescheduleInput input,string key,CancellationToken ct);
    Task<IdempotentCommandResult> CancelAsync(Guid id,long expectedRevision,AppointmentCancelInput input,string key,CancellationToken ct);
}