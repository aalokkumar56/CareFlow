using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientIdentityReviewVersion:TenantEntity
{
    public Guid ReviewId{get;set;}
    public Guid FirstPatientId{get;set;}
    public Guid SecondPatientId{get;set;}
    public long Revision{get;set;}
    public string Decision{get;set;}="open";
    public string EvidenceReference{get;set;}="";
    public string Reason{get;set;}="";
    public string FirstPatientTag{get;set;}="";
    public string SecondPatientTag{get;set;}="";
    public bool ExactNameSignal{get;set;}
    public bool SharedPhoneSignal{get;set;}
}