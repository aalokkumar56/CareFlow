using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientIdentifierCounter:TenantEntity
{
    public Guid FacilityId{get;set;}
    public string Namespace{get;set;}="local-mrn";
    public long CurrentValue{get;set;}
}
public sealed class PatientIdentifier:TenantEntity
{
    public Guid FacilityId{get;set;}
    public Guid PatientId{get;set;}
    public string Namespace{get;set;}="local-mrn";
    public string Value{get;set;}="";
    public long Sequence{get;set;}
}