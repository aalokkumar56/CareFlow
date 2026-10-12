using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Common;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Organization;

public sealed class OrganizationHierarchyService(ICureFlowDbSession db, ITenantContext tenant,
    IUserPermissionService permissions, IIdempotentCommandExecutor commands, IClinicalAccessAuditService audit, IConfiguration config) : IOrganizationHierarchyService
{
    private async Task RequireAsync(ICureFlowDbSession store, Guid facility, string capability, CancellationToken ct)
    {
        if (!tenant.IsAuthenticated || tenant.IsPlatformUser || !tenant.UserId.HasValue || tenant.TenantId == Guid.Empty)
            throw new DomainException("A current hospital actor is required.",403);
        var actor = await store.GetByIdAsync<User>(tenant.UserId.Value, ct: ct);
        if (actor is null || !actor.IsActive || !(await permissions.GetPermissionCodesAsync(actor, ct, store)).Contains(capability,StringComparer.Ordinal))
            throw new DomainException("You do not have permission to manage organization structure.",403);
        var owner = await store.QueryFirstOrDefaultAsync<Facility>("""
            SELECT * FROM "Facilities" WHERE "Id"=@facility AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted"
            """,new { facility },ct:ct);
        if(owner is null) throw new NotFoundException("Facility");
    }
    public async Task<IReadOnlyList<OrganizationStructureVersion>> HistoryAsync(Guid facilityId,string category,Guid id,long beforeRevision,int limit,CancellationToken ct)
    {
        if(category is not ("units" or "locations"))throw new NotFoundException("Organization category");
        await RequireAsync(db,facilityId,CureFlowPermissions.OrganizationView,ct);
        var rows=await db.QueryAsync<OrganizationStructureVersion>("""
            SELECT * FROM "OrganizationStructureVersions" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "NodeCategory"=@category AND "NodeId"=@id
              AND (@beforeRevision=0 OR "Revision"<@beforeRevision) ORDER BY "Revision" DESC LIMIT @limit
            """,new{facilityId,category,id,beforeRevision=Math.Max(0,beforeRevision),limit=Math.Clamp(limit,1,100)},ct:ct);
        await audit.RecordAsync(new(null,"OrganizationStructure",id,"inspect_history","authorized",Purpose:"operations"),ct);
        return rows;
    }
    public Task<IReadOnlyList<OrganizationNodeView>> ChildrenAsync(Guid facilityId,string category,Guid? parentId,int limit,CancellationToken ct) =>
        category switch { "units"=>ChildrenAsync<OrganizationUnit>(facilityId,parentId,limit,ct),
            "locations"=>ChildrenAsync<FacilityLocation>(facilityId,parentId,limit,ct), _=>throw new NotFoundException("Organization category") };
    private async Task<IReadOnlyList<OrganizationNodeView>> ChildrenAsync<T>(Guid facility,Guid? parent,int limit,CancellationToken ct)
        where T:TenantEntity,IOrganizationNode,new()
    {
        await RequireAsync(db,facility,CureFlowPermissions.OrganizationView,ct);
        var table=Table<T>();
        if(parent.HasValue && await NodeAsync<T>(db,facility,parent.Value,ct) is null) throw new NotFoundException("Parent");
        var rows=await db.QueryAsync<T>($"""
            SELECT * FROM "{table}" WHERE "TenantId"=@TenantId AND "FacilityId"=@facility AND NOT "IsDeleted"
              AND "ParentId" IS NOT DISTINCT FROM @parent ORDER BY "Code","Id" LIMIT @limit
            """,new {facility,parent,limit=Math.Clamp(limit,1,200)},ct:ct);
        await audit.RecordAsync(new(null,"OrganizationStructure",parent,"inspect","authorized",Purpose:"operations"),ct);
        return rows.Select(View).ToList();
    }
    public Task<IdempotentCommandResult> SaveAsync(Guid facilityId,string category,Guid? id,OrganizationNodeSave request,string key,CancellationToken ct) =>
        category switch { "units"=>SaveAsync<OrganizationUnit>(facilityId,id,request,key,ct),
            "locations"=>SaveAsync<FacilityLocation>(facilityId,id,request,key,ct), _=>throw new NotFoundException("Organization category") };
    private Task<IdempotentCommandResult> SaveAsync<T>(Guid facility,Guid? id,OrganizationNodeSave input,string key,CancellationToken ct)
        where T:TenantEntity,IOrganizationNode,new()
    {
        if(string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>200||input.Name.Any(char.IsControl)
            ||string.IsNullOrWhiteSpace(input.Code)||input.Code.Length>64||input.Code.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_')))
            throw new ValidationException("Supply a bounded organization name and stable code.");
        var allowed=typeof(T)==typeof(OrganizationUnit) ? new[]{"department","unit"} : new[]{"location"};
        if(!allowed.Contains(input.Kind,StringComparer.Ordinal))throw new ValidationException("Select a supported organization kind.");
        if(id.HasValue && input.ExpectedRevision is not >0)throw new DomainException("Supply the current organization revision.",428);
        if(!id.HasValue && input.ExpectedRevision.HasValue)throw new ValidationException("New organization nodes have no prior revision.");
        var horizon=config.GetValue<int?>("OrganizationHierarchy:ApprovedRetryHorizonHours");
        if(horizon is not >0 or >8760)throw new DomainException("Organization command retry policy is not configured.",503);
        return commands.ExecuteAsync("organization."+Table<T>().ToLowerInvariant()+".save",key,new{facility,id,input},TimeSpan.FromHours(horizon.Value),
            async(store,token)=>await RequireAsync(store,facility,CureFlowPermissions.OrganizationManage,token),
            async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",
                    new{scope=$"organization-db:{tenant.TenantId:D}:{facility:D}"},ct:token);
                // Parent facility lifecycle changes serialize with the same locked facility row.
                var active=await store.QueryFirstOrDefaultAsync<Guid?>("""
                    SELECT "Id" FROM "Facilities" WHERE "Id"=@facility AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" AND public.cureflow_lock_active_facility(@TenantId,@facility)="Id"
                    """,new{facility},ct:token);
                if(!active.HasValue)throw new NotFoundException("Facility");
                var row=id.HasValue ? await NodeAsync<T>(store,facility,id.Value,token) ?? throw new NotFoundException("Organization node") : new T{FacilityId=facility};
                if(id.HasValue && row.Revision!=input.ExpectedRevision)throw new DomainException("Organization node changed; reload its revision.",412);
                if(id.HasValue && !StringComparer.Ordinal.Equals(row.Code,input.Code.Trim().ToUpperInvariant()))throw new DomainException("Retained organization codes cannot be changed.",409);
                if(row.Id==input.ParentId)throw new DomainException("An organization node cannot parent itself.",409);
                var table=Table<T>();
                if(input.ParentId.HasValue)
                {
                    var parent=await NodeAsync<T>(store,facility,input.ParentId.Value,token)??throw new NotFoundException("Parent");
                    if(!parent.IsActive)throw new DomainException("Parent must be active.",409);
                    var ancestors=await store.QueryAsync<Ancestor>($"""
                        WITH RECURSIVE ancestry AS (
                          SELECT "Id","ParentId",1 AS "Depth",ARRAY["Id"] AS path,false AS "Cycle" FROM "{table}"
                           WHERE "Id"=@parent AND "FacilityId"=@facility AND "TenantId"=@TenantId
                          UNION ALL
                          SELECT p."Id",p."ParentId",a."Depth"+1,a.path||p."Id",p."Id"=ANY(a.path)
                           FROM "{table}" p JOIN ancestry a ON p."Id"=a."ParentId"
                           WHERE p."FacilityId"=@facility AND p."TenantId"=@TenantId AND a."Depth"<32 AND NOT a."Cycle")
                        SELECT "Id","ParentId","Depth","Cycle" FROM ancestry
                        """,new{parent=input.ParentId,facility},ct:token);
                    if(ancestors.Any(x=>x.Id==row.Id||x.Cycle||x.Depth>=32))throw new DomainException("Hierarchy cycle/depth requires review.",409);
                }
                if(!input.IsActive)
                {
                    var child=await store.QueryFirstOrDefaultAsync<Guid?>($"""
                        SELECT "Id" FROM "{table}" WHERE "ParentId"=@id AND "TenantId"=@TenantId AND "FacilityId"=@facility AND "IsActive" AND NOT "IsDeleted" LIMIT 1
                        """,new{id=row.Id,facility},ct:token);
                    if(child.HasValue)throw new DomainException("Deactivate or move active children first.",409);
                }
                var duplicate=await store.QueryFirstOrDefaultAsync<Guid?>($"""
                    SELECT "Id" FROM "{table}" WHERE "TenantId"=@TenantId AND "FacilityId"=@facility AND "Code"=@code AND "Id"<>@id LIMIT 1
                    """,new{facility,code=input.Code.Trim().ToUpperInvariant(),id=row.Id},ct:token);
                if(duplicate.HasValue)throw new DomainException("Organization code is already retained in this facility.",409);
                row.Name=input.Name.Trim();row.Code=input.Code.Trim().ToUpperInvariant();row.Kind=input.Kind;row.ParentId=input.ParentId;row.IsActive=input.IsActive;
                if(id.HasValue){row.Revision++;await store.UpdateAsync(row,ct:token);}else await store.InsertAsync(row,ct:token);
                await audit.RecordAsync(new(null,typeof(T).Name,row.Id,id.HasValue?"update":"create","committed",row.Revision.ToString(),"operations"),token,store);
                return(new CommandReceipt(row.Id,row.Revision,id.HasValue?CommandOutcome.Updated:CommandOutcome.Created),id.HasValue?200:201);
            },ct);
    }
    private static string Table<T>()=>typeof(T)==typeof(OrganizationUnit)?"OrganizationUnits":"FacilityLocations";
    private static Task<T?> NodeAsync<T>(ICureFlowDbSession store,Guid facility,Guid id,CancellationToken ct) where T:TenantEntity,IOrganizationNode,new()=>store.QueryFirstOrDefaultAsync<T>(
        $"SELECT * FROM \"{Table<T>()}\" WHERE \"Id\"=@id AND \"TenantId\"=@TenantId AND \"FacilityId\"=@facility AND NOT \"IsDeleted\"",new{id,facility},ct:ct);
    private static OrganizationNodeView View<T>(T node) where T:TenantEntity,IOrganizationNode =>
        new(node.Id,node.FacilityId,node.ParentId,node.Name,node.Code,node.Kind,node.IsActive,node.Revision);
    private sealed class Ancestor {public Guid Id{get;set;}public Guid? ParentId{get;set;}public int Depth{get;set;}public bool Cycle{get;set;}}
}
