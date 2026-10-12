namespace CureFlow.Application.Interfaces;
public record AppointmentNoShowInput(Guid AssignedUserId,Guid EscalationOwnerUserId,DateTime FollowUpDueAtUtc,string Reason);
public record AppointmentNoShowView(Guid Id,Guid AppointmentId,Guid PatientId,Guid FacilityId,Guid FollowUpWorkId,
    long OriginalAppointmentRevision,long AppointmentRevision,DateTime ScheduledAtUtc,int CutoffMinutesAfterStart,
    DateTime RecordedAtUtc,string Reason,string PolicyVersion,Guid? Actor);
public interface IAppointmentNoShowService
{
    Task<IdempotentCommandResult> RecordAsync(Guid facilityId,Guid appointmentId,long expectedRevision,
        AppointmentNoShowInput input,string key,CancellationToken ct);
    Task<AppointmentNoShowView> GetAsync(Guid facilityId,Guid appointmentId,CancellationToken ct);
}
