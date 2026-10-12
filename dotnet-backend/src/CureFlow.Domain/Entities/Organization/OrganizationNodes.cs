using CureFlow.Domain.Common;
namespace CureFlow.Domain.Entities.Organization;

public interface IOrganizationNode
{
    Guid FacilityId { get; set; }
    Guid? ParentId { get; set; }
    string Name { get; set; }
    string Code { get; set; }
    string Kind { get; set; }
    long Revision { get; set; }
}
public sealed class OrganizationUnit : TenantEntity, IOrganizationNode
{
    public Guid FacilityId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Kind { get; set; } = "unit";
    public long Revision { get; set; } = 1;
}
public sealed class FacilityLocation : TenantEntity, IOrganizationNode
{
    public Guid FacilityId { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Kind { get; set; } = "location";
    public long Revision { get; set; } = 1;
}
