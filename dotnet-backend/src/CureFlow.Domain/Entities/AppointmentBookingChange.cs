using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class AppointmentBookingChange:TenantEntity
{
    public Guid AppointmentId{get;set;}
    public Guid PatientId{get;set;}
    public long AppointmentRevision{get;set;}
    public string Action{get;set;}="";
    public Guid PreviousReservationId{get;set;}
    public Guid? ReplacementReservationId{get;set;}
    public DateTime PreviousStartsAtUtc{get;set;}
    public DateTime PreviousEndsAtUtc{get;set;}
    public DateTime? ReplacementStartsAtUtc{get;set;}
    public DateTime? ReplacementEndsAtUtc{get;set;}
    public string Reason{get;set;}="";
    public string PolicyVersion{get;set;}="";
}