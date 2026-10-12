using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
/// <summary>Local contact history only; no account, proxy or sharing authority.</summary>
public sealed class PatientContactVersion:TenantEntity
{
    public Guid PatientId{get;set;}
    public Guid ContactId{get;set;}
    public long Revision{get;set;}
    public string ContactType{get;set;}="person";
    public string Usage{get;set;}="normal";
    public string DisplayName{get;set;}="";
    public string? Value{get;set;}
    public string? Relationship{get;set;}
    public string? SourceReference{get;set;}
    public string VerificationStatus{get;set;}="unverified";
    public bool Preferred{get;set;}
    public bool ContactIsActive{get;set;}=true;
    public DateTime EffectiveFrom{get;set;}
    public DateTime? EffectiveUntil{get;set;}
}