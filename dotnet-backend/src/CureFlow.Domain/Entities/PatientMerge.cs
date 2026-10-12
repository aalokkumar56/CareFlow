using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities;
public sealed class PatientMerge:TenantEntity
{
    public Guid SourcePatientId{get;set;}
    public Guid SurvivorPatientId{get;set;}
    public Guid ReviewVersionId{get;set;}
    public string SourcePatientTag{get;set;}="";
    public string SurvivorPatientTag{get;set;}="";
    public string Reason{get;set;}="";
    public string PolicyVersion{get;set;}="";
    public DateTime MergedAt{get;set;}
    public long Revision{get;set;}=1;
    public DateTime? ReversedAt{get;set;}
    public string? ReversalReason{get;set;}
}
public sealed class PatientLinkJournal:TenantEntity
{
    public Guid MergeId{get;set;}
    public string Operation{get;set;}="merge";
    public string EntityType{get;set;}="PatientIdentityMapping";
    public Guid EntityId{get;set;}
    public string BeforeValue{get;set;}="{}";
    public string AfterValue{get;set;}="{}";
    public DateTime AppliedAt{get;set;}
    public bool PublicationHold{get;set;}
}
