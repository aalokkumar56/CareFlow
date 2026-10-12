using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
// Operational occupancy only: no patient name/contact/chart identity is held here.
public sealed class AppointmentReservation:TenantEntity
{
    public Guid OwnerUserId{get;set;}
    public Guid FacilityId{get;set;}
    public Guid PractitionerUserId{get;set;}
    public DateTime StartsAtUtc{get;set;}
    public DateTime EndsAtUtc{get;set;}
    public DateTime HoldExpiresAtUtc{get;set;}
    public string State{get;set;}="held";
    public long Revision{get;set;}=1;
    public string PolicyVersion{get;set;}="";
    public Guid? AppointmentId{get;set;}
}
public sealed class AppointmentReservationSeat:TenantEntity
{
    public Guid ReservationId{get;set;}
    public string TargetType{get;set;}="";
    public Guid TargetId{get;set;}
    public int Seat{get;set;}
    public int CapacitySnapshot{get;set;}
    public DateTime StartsAtUtc{get;set;}
    public DateTime EndsAtUtc{get;set;}
    public string State{get;set;}="held";
}