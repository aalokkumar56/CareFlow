using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Organization;
/// <summary>Immutable structure snapshot; historical care/source ownership is never rewritten.</summary>
public sealed class OrganizationStructureVersion:TenantEntity
{
    public Guid FacilityId{get;set;}
    public Guid NodeId{get;set;}
    public string NodeCategory{get;set;}="";
    public long Revision{get;set;}
    public DateTime EffectiveFrom{get;set;}
    public Guid? ParentId{get;set;}
    public string Name{get;set;}="";
    public string Code{get;set;}="";
    public string Kind{get;set;}="";
    public bool NodeIsActive{get;set;}
}