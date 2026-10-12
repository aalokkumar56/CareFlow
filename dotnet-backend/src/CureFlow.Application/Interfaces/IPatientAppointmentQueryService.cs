namespace CureFlow.Application.Interfaces;
public record PatientAppointmentCursor(DateTime ScheduledAtUtc,Guid AppointmentId);
public record PatientAppointmentQuery(DateTime FromUtc,DateTime ToUtc,Guid? FacilityId,string? Status,int Limit,
 DateTime? AfterScheduledAtUtc,Guid? AfterAppointmentId);
public record PatientAppointmentView(Guid Id,Guid PatientId,Guid? FacilityId,Guid? PractitionerUserId,
 DateTime ScheduledAtUtc,DateTimeOffset? ScheduledAtLocal,string? TimeZoneId,int DurationMinutes,string Status,long Revision);
public record PatientAppointmentPage(IReadOnlyList<PatientAppointmentView> Items,PatientAppointmentCursor? NextCursor,DateTime FromUtc,DateTime ToUtc);
public record PatientNextAppointment(DateTime EvaluatedAtUtc,PatientAppointmentView? Appointment);
public interface IPatientAppointmentQueryService
{
 Task<PatientAppointmentPage> ListAsync(Guid patientId,PatientAppointmentQuery query,CancellationToken ct);
 Task<PatientNextAppointment> NextAsync(Guid patientId,Guid? facilityId,CancellationToken ct);
}
