using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class EncounterTransition : TenantEntity
{
 public Guid VisitId{get;set;}
 public Guid PatientId{get;set;}
 public Guid? AppointmentId{get;set;}
 public long Revision{get;set;}
 public int? PreviousStatus{get;set;}
 public int Status{get;set;}
 public DateTime StartedAtUtc{get;set;}
 public DateTime? EndedAtUtc{get;set;}
 public string Action{get;set;}="";
 public string Reason{get;set;}="";
 public string PolicyVersion{get;set;}="";
 public long? SourceAppointmentRevision{get;set;}
 public string ContentSnapshotJson{get;set;}="{}";
}
