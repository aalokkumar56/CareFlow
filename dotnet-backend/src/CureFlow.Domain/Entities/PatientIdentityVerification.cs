using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class TemporaryPatientIdentity:TenantEntity
{
    public Guid PatientId{get;set;}
    public Guid FacilityId{get;set;}
    public string Value{get;set;}="";
    public string Reason{get;set;}="";
    public string? SourceReference{get;set;}
}
public sealed class PatientIdentityVerification:TenantEntity
{
    public Guid PatientId{get;set;}
    public Guid DemographicVersionId{get;set;}
    public string ProofReference{get;set;}="";
    public string Reason{get;set;}="";
    public string PolicyVersion{get;set;}="";
    public string PatientTag{get;set;}="";
}
