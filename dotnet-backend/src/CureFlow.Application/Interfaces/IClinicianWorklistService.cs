namespace CureFlow.Application.Interfaces;
public record ClinicianWorklistQuery(DateOnly Date,int Limit=50,DateTime? AfterScheduledAtUtc=null,Guid? AfterAppointmentId=null);
public record ClinicianWorklistItem(Guid AppointmentId,Guid PatientId,long AppointmentRevision,DateTime ScheduledAtUtc,
 Guid? QueueEntryId,long? ArrivalToken,DateTime? ArrivedAtUtc,Guid? EncounterId,long? EncounterRevision,string State,bool RequiresReconciliation);
public record ClinicianWorklistCursor(DateTime ScheduledAtUtc,Guid AppointmentId);
public record ClinicianWorklistPage(Guid FacilityId,Guid PractitionerUserId,DateOnly Date,string Timezone,Guid OperatingSettingId,
 DateTime FromUtc,DateTime ToUtc,IReadOnlyList<ClinicianWorklistItem> Items,ClinicianWorklistCursor? NextCursor);
public interface IClinicianWorklistService
{
 Task<ClinicianWorklistPage> ListAsync(Guid facilityId,ClinicianWorklistQuery query,CancellationToken ct);
}
