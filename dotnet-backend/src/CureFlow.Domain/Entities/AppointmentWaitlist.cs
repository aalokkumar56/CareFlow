using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class AppointmentWaitlistEntry:TenantEntity
{
    public Guid PatientId{get;set;}
    public Guid FacilityId{get;set;}
    public Guid PractitionerUserId{get;set;}
    public string ResourceIdsJson{get;set;}="[]";
    public DateTime EarliestStartsAtUtc{get;set;}
    public DateTime LatestEndsAtUtc{get;set;}
    public int DurationMinutes{get;set;}
    public DateTime ExpiresAtUtc{get;set;}
    public string BookingKind{get;set;}="routine";
    public Guid? FollowUpAppointmentId{get;set;}
    public string State{get;set;}="waiting";
    public long Revision{get;set;}=1;
    public Guid? CurrentOfferId{get;set;}
    public string PolicyVersion{get;set;}="";
}
public sealed class AppointmentWaitlistOffer:TenantEntity
{
    public Guid EntryId{get;set;}
    public Guid PatientId{get;set;}
    public Guid ReleasedReservationId{get;set;}
    public Guid ReservationId{get;set;}
    public Guid OwnerUserId{get;set;}
    public DateTime ExpiresAtUtc{get;set;}
    public string State{get;set;}="offered";
    public long Revision{get;set;}=1;
    public Guid? AppointmentId{get;set;}
    public string PolicyVersion{get;set;}="";
}
public sealed class AppointmentWaitlistDecision:TenantEntity
{
    public Guid EntryId{get;set;}
    public Guid PatientId{get;set;}
    public long EntryRevision{get;set;}
    public Guid? OfferId{get;set;}
    public string Action{get;set;}="";
    public string Reason{get;set;}="";
}