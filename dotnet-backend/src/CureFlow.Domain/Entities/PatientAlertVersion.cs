using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientConfidentialityState:TenantEntity
{
    public Guid PatientId{get;set;}
    public bool Restricted{get;set;}
    public long Revision{get;set;}
}
public sealed class PatientAlertVersion:TenantEntity
{
    public Guid AlertId{get;set;}
    public Guid PatientId{get;set;}
    public string Key{get;set;}="";
    public string Category{get;set;}="";
    public long Revision{get;set;}
    public string Title{get;set;}="";
    public string? Detail{get;set;}
    public string Severity{get;set;}="info";
    public bool AlertIsActive{get;set;}
    public string SourceType{get;set;}="";
    public string? SourceReference{get;set;}
    public string Reason{get;set;}="";
    public DateOnly? ReportedDeathDate{get;set;}
    public DateTime EffectiveFrom{get;set;}
    public string PolicyVersion{get;set;}="";
}
