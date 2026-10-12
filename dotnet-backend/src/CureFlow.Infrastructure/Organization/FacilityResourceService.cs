using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Organization;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Organization;
public sealed class FacilityResourceService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IFacilityResourceService
{
    private async Task RequireAsync(ICureFlowDbSession store,Guid facility,string capability,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new DomainException("Current hospital actor required.",403);
        var user=await store.GetByIdAsync<User>(tenant.UserId.Value,ct:ct);
        if(user is null||!user.IsActive||!(await permissions.GetPermissionCodesAsync(user,ct,store)).Contains(capability,StringComparer.Ordinal))throw new DomainException("Resource permission required.",403);
        var owner=await store.QueryFirstOrDefaultAsync<Guid?>("""
          SELECT "Id" FROM "Facilities" WHERE "Id"=@facility AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted"
          """,new{facility},ct:ct);
        if(!owner.HasValue)throw new NotFoundException("Facility");
    }
    public async Task<IReadOnlyList<FacilityResourceView>> ListAsync(Guid facilityId,bool eligibleOnly,int limit,CancellationToken ct)
    {
        await RequireAsync(db,facilityId,CureFlowPermissions.OrganizationView,ct);
        var rows=await db.QueryAsync<ResourceProjection>("""
          SELECT r.*, (r."IsActive" AND r."OperationalStatus"='enabled' AND l."IsActive" AND NOT l."IsDeleted"
            AND r."EffectiveFrom"<=@now AND (r."EffectiveUntil" IS NULL OR r."EffectiveUntil">@now)) AS "Eligible"
          FROM "FacilityResources" r JOIN "FacilityLocations" l ON l."TenantId"=r."TenantId" AND l."FacilityId"=r."FacilityId" AND l."Id"=r."LocationId"
          WHERE r."TenantId"=@TenantId AND r."FacilityId"=@facilityId AND NOT r."IsDeleted"
            AND (NOT @eligibleOnly OR (r."IsActive" AND r."OperationalStatus"='enabled' AND l."IsActive" AND NOT l."IsDeleted" AND r."EffectiveFrom"<=@now AND (r."EffectiveUntil" IS NULL OR r."EffectiveUntil">@now)))
          ORDER BY r."Code",r."Id" LIMIT @limit
          """,new{facilityId,eligibleOnly,now=DateTime.UtcNow,limit=Math.Clamp(limit,1,200)},ct:ct);
        await audit.RecordAsync(new(null,"FacilityResources",facilityId,"inspect","authorized",Purpose:"operations"),ct);
        return rows.Select(r=>new FacilityResourceView(r.Id,r.FacilityId,r.LocationId,r.Name,r.Code,r.ResourceType,r.Capacity,r.OperationalStatus,r.EffectiveFrom,r.EffectiveUntil,r.Revision,r.Eligible)).ToList();
    }
    public Task<IdempotentCommandResult> SaveAsync(Guid facilityId,Guid? id,FacilityResourceSave input,string key,CancellationToken ct)
    {
        if(input.LocationId==Guid.Empty||string.IsNullOrWhiteSpace(input.Name)||input.Name.Length>200||input.Name.Any(char.IsControl)
          ||string.IsNullOrWhiteSpace(input.Code)||input.Code.Length>64||input.Code.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))
          ||input.Capacity is <1 or >100000||string.IsNullOrWhiteSpace(input.ResourceType)||input.ResourceType.Length>32
          ||input.ResourceType.Any(c=>!(char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))
          ||input.OperationalStatus is not ("enabled" or "disabled" or "maintenance")
          ||input.EffectiveFrom.Kind!=DateTimeKind.Utc||input.EffectiveUntil.HasValue&&(input.EffectiveUntil.Value.Kind!=DateTimeKind.Utc||input.EffectiveUntil<=input.EffectiveFrom))
            throw new ValidationException("Supply bounded resource fields, positive capacity and a UTC eligibility interval.");
        if(id.HasValue&&input.ExpectedRevision is not >0)throw new DomainException("Supply current resource revision.",428);
        if(!id.HasValue&&input.ExpectedRevision.HasValue)throw new ValidationException("New resource has no prior revision.");
        var horizon=config.GetValue<int?>("FacilityResources:ApprovedRetryHorizonHours");
        if(horizon is not >0 or >8760)throw new DomainException("Resource retry policy is not configured.",503);
        return commands.ExecuteAsync("facility.resource.save",key,new{facilityId,id,input},TimeSpan.FromHours(horizon.Value),
            (store,token)=>RequireAsync(store,facilityId,CureFlowPermissions.OrganizationManage,token),async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=$"organization-db:{tenant.TenantId:D}:{facilityId:D}"},ct:token);
                var owner=await store.QueryFirstOrDefaultAsync<Guid?>("""
                  SELECT "Id" FROM "Facilities" WHERE "Id"=@facilityId AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" AND public.cureflow_lock_active_facility(@TenantId,@facilityId)="Id"
                  """,new{facilityId},ct:token);
                if(!owner.HasValue)throw new NotFoundException("Facility");
                var location=await store.QueryFirstOrDefaultAsync<Guid?>("""
                  SELECT "Id" FROM "FacilityLocations" WHERE "Id"=@location AND "FacilityId"=@facilityId AND "TenantId"=@TenantId AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                  """,new{location=input.LocationId,facilityId},ct:token);
                if(!location.HasValue)throw new NotFoundException("Active facility location");
                var row=id.HasValue?await store.QueryFirstOrDefaultAsync<FacilityResource>("""
                    SELECT * FROM "FacilityResources" WHERE "Id"=@id AND "FacilityId"=@facilityId AND "TenantId"=@TenantId AND NOT "IsDeleted" FOR UPDATE
                    """,new{id,facilityId},ct:token)??throw new NotFoundException("Resource"):new FacilityResource{FacilityId=facilityId};
                if(id.HasValue&&row.Revision!=input.ExpectedRevision)throw new DomainException("Resource changed; reload.",412);
                var code=input.Code.Trim().ToUpperInvariant();
                if(id.HasValue&&row.Code!=code)throw new DomainException("Retained resource code cannot change.",409);
                var duplicate=await store.QueryFirstOrDefaultAsync<Guid?>("""
                    SELECT "Id" FROM "FacilityResources" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Code"=@code AND "Id"<>@id LIMIT 1
                    """,new{facilityId,code,id=row.Id},ct:token);
                if(duplicate.HasValue)throw new DomainException("Resource code is retained in this facility.",409);
                row.LocationId=input.LocationId;row.Name=input.Name.Trim();row.Code=code;row.ResourceType=input.ResourceType.ToLowerInvariant();
                row.Capacity=input.Capacity;row.OperationalStatus=input.OperationalStatus;row.EffectiveFrom=input.EffectiveFrom;row.EffectiveUntil=input.EffectiveUntil;
                if(id.HasValue){row.Revision++;await store.UpdateAsync(row,ct:token);}else await store.InsertAsync(row,ct:token);
                await audit.RecordAsync(new(null,"FacilityResource",row.Id,id.HasValue?"update":"create","committed",row.Revision.ToString(),"operations"),token,store);
                return(new CommandReceipt(row.Id,row.Revision,id.HasValue?CommandOutcome.Updated:CommandOutcome.Created),id.HasValue?200:201);
            },ct);
    }
    private sealed class ResourceProjection
    {
        public Guid Id{get;set;} public Guid FacilityId{get;set;} public Guid LocationId{get;set;} public string Name{get;set;}="";
        public string Code{get;set;}="";public string ResourceType{get;set;}="";public int Capacity{get;set;}public string OperationalStatus{get;set;}="";
        public DateTime EffectiveFrom{get;set;}public DateTime? EffectiveUntil{get;set;}public long Revision{get;set;}public bool Eligible{get;set;}
    }
}