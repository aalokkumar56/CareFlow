namespace CureFlow.Application.Interfaces;
public record AppointmentWaitlistInput(Guid PatientId,Guid FacilityId,Guid PractitionerUserId,IReadOnlyList<Guid> ResourceIds,
 DateTime EarliestStartsAtUtc,DateTime LatestEndsAtUtc,int DurationMinutes,string BookingKind,Guid? FollowUpAppointmentId,string Reason);
public record AppointmentWaitlistOfferInput(long ExpectedRevision,Guid ReleasedReservationId,Guid ReservationId,long ExpectedReservationRevision,string Reason);
public record AppointmentWaitlistAcceptanceInput(long ExpectedRevision,long ExpectedOfferRevision,string Reason);
public record AppointmentWaitlistWithdrawalInput(long ExpectedRevision,string Reason);
public record AppointmentWaitlistOfferView(Guid Id,Guid ReservationId,DateTime ExpiresAtUtc,string State,long Revision);
public record AppointmentWaitlistView(Guid Id,Guid PatientId,Guid FacilityId,Guid PractitionerUserId,IReadOnlyList<Guid> ResourceIds,
 DateTime EarliestStartsAtUtc,DateTime LatestEndsAtUtc,int DurationMinutes,string BookingKind,Guid? FollowUpAppointmentId,
 DateTime ExpiresAtUtc,string State,long Revision,AppointmentWaitlistOfferView? Offer);
public interface IAppointmentWaitlistService
{
 Task<IdempotentCommandResult> AddAsync(AppointmentWaitlistInput input,string key,CancellationToken ct);
 Task<AppointmentWaitlistView> GetAsync(Guid id,CancellationToken ct);
 Task<IdempotentCommandResult> OfferAsync(Guid id,AppointmentWaitlistOfferInput input,string key,CancellationToken ct);
 Task<IdempotentCommandResult> AcceptAsync(Guid id,Guid offerId,AppointmentWaitlistAcceptanceInput input,string key,CancellationToken ct);
 Task<IdempotentCommandResult> WithdrawAsync(Guid id,AppointmentWaitlistWithdrawalInput input,string key,CancellationToken ct);
}