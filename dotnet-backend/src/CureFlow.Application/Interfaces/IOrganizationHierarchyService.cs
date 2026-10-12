namespace CureFlow.Application.Interfaces;

public record OrganizationNodeSave(string Name, string Code, string Kind, Guid? ParentId, bool IsActive, long? ExpectedRevision);
public record OrganizationNodeView(Guid Id, Guid FacilityId, Guid? ParentId, string Name, string Code, string Kind, bool IsActive, long Revision);
public interface IOrganizationHierarchyService
{
    Task<IReadOnlyList<CureFlow.Domain.Entities.Organization.OrganizationStructureVersion>> HistoryAsync(Guid facilityId,string category,Guid id,long beforeRevision,int limit,CancellationToken ct);
    Task<IReadOnlyList<OrganizationNodeView>> ChildrenAsync(Guid facilityId, string category, Guid? parentId, int limit, CancellationToken ct);
    Task<IdempotentCommandResult> SaveAsync(Guid facilityId, string category, Guid? id, OrganizationNodeSave request, string key, CancellationToken ct);
}
