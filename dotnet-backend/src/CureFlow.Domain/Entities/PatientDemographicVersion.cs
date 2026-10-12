using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientDemographicVersion:TenantEntity
{
    public Guid PatientId{get;set;}
    public long Revision{get;set;}
    public string SourceType{get;set;}="local_application";
    public string? SourceReference{get;set;}
    public string Reason{get;set;}="";
    public string VerificationStatus{get;set;}="unverified";
    public string? BeforeValue{get;set;}
    public string AfterValue{get;set;}="{}";
}
