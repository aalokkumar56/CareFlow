using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class LeadConversion:TenantEntity
{
    public Guid LeadId{get;set;}
    public Guid PatientId{get;set;}
    public Guid FacilityId{get;set;}
    public string Decision{get;set;}="";
    public string SourceLeadTag{get;set;}="";
    public string OriginalLeadJson{get;set;}="";
    public string CandidateReviewTag{get;set;}="";
    public string? SelectedPatientTag{get;set;}
    public string EvidenceReference{get;set;}="";
    public string Reason{get;set;}="";
    public string? ReferralSource{get;set;}
    public Guid? ReferringDoctorId{get;set;}
    public string PolicyVersion{get;set;}="";
}