namespace CureFlow.Application.Interfaces;
public record EncounterCompletionInput(long ExpectedRevision,string Reason);
public record EncounterResumeInput(long ExpectedRevision);
public record EncounterState(Guid Id,Guid PatientId,Guid? AppointmentId,Guid DoctorUserId,long Revision,string Status,DateTime StartedAtUtc,DateTime? EndedAtUtc);
public record EncounterTransitionView(long Revision,string Action,string Reason,Guid? ActorUserId,DateTime RecordedAtUtc,int? PreviousStatus,int Status,DateTime? EndedAtUtc);
