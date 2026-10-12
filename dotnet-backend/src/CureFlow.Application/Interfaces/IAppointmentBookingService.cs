using CureFlow.Domain.Enums;
namespace CureFlow.Application.Interfaces;
public record AppointmentBookingInput(Guid PatientId,Guid ReservationId,long ExpectedReservationRevision,string BookingKind,
    Guid? FollowUpAppointmentId,string? ChiefComplaint,string? Notes);
public record AppointmentBookingView(Guid Id,Guid PatientId,Guid FacilityId,Guid ReservationId,Guid PractitionerUserId,
    DateTime StartsAtUtc,DateTime EndsAtUtc,string BookingKind,Guid? FollowUpAppointmentId,AppointmentStatus Status,long Revision,string ETag);
public interface IAppointmentBookingService
{
    Task<IdempotentCommandResult> BookAsync(AppointmentBookingInput input,string key,CancellationToken ct);
    Task<CommandReceipt> BookInTransactionAsync(ICureFlowDbSession store,AppointmentBookingInput input,CancellationToken ct);
    Task<AppointmentBookingView> GetAsync(Guid id,CancellationToken ct);
}