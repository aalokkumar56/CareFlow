using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientIdentifierService(ITenantContext tenant,IUserPermissionService permissions,IClinicalAccessAuditService audit):IPatientIdentifierService
{
    public async Task<IReadOnlyList<PatientIdentifierView>> ListAsync(ICureFlowDbSession store,Guid facilityId,Guid patientId,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||!tenant.UserId.HasValue||tenant.TenantId==Guid.Empty||store.TenantId!=tenant.TenantId)throw new ForbiddenException();
        var actor=await store.GetByIdAsync<User>(tenant.UserId.Value,ct:ct);
        if(actor is null||!actor.IsActive||!(await permissions.GetPermissionCodesAsync(actor,ct,store)).Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal))throw new ForbiddenException();
        _=await store.GetByIdAsync<Patient>(patientId,ct:ct)??throw new NotFoundException("Patient");
        var rows=await store.QueryAsync<PatientIdentifier>("""
            SELECT * FROM "PatientIdentifiers" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "PatientId"=@patientId ORDER BY "Namespace","Id" LIMIT 20
            """,new{facilityId,patientId},ct:ct);
        await audit.RecordAsync(new(patientId,"PatientIdentifier",null,"read","authorized",Purpose:"registration"),ct,store);
        return rows.Select(r=>new PatientIdentifierView(r.Id,r.FacilityId,r.Namespace,r.Value)).ToList();
    }
    public async Task<PatientIdentifier> AllocateLocalMrnAsync(ICureFlowDbSession store,Guid facilityId,Guid patientId,CancellationToken ct)
    {
        if(store.TenantId!=tenant.TenantId||store.UserId!=tenant.UserId)throw new ForbiddenException();
        if(!store.IsInTransaction)throw new InvalidOperationException("Identifier allocation requires the source registration transaction.");
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.GetByIdAsync<User>(tenant.UserId.Value,ct:ct);
        if(actor is null||!actor.IsActive||!(await permissions.GetPermissionCodesAsync(actor,ct,store)).Contains(CureFlowPermissions.PatientCreate,StringComparer.Ordinal))throw new ForbiddenException();
        var facility=await store.QueryFirstOrDefaultAsync<Guid?>("""
            SELECT public.cureflow_lock_active_facility(@TenantId,@facilityId)
            """,new{facilityId},ct:ct);
        if(!facility.HasValue)throw new NotFoundException("Active facility");
        var patient=await store.QueryFirstOrDefaultAsync<Guid?>("""
            SELECT "Id" FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=@patientId AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{patientId},ct:ct);
        if(!patient.HasValue)throw new NotFoundException("Active patient");
        // Serialize same-patient allocation as well as each namespace counter; existing identities are returned, never reused.
        await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=$"local-mrn:{tenant.TenantId:D}:{facilityId:D}:{patientId:D}"},ct:ct);
        var existing=await store.QueryFirstOrDefaultAsync<PatientIdentifier>("""
            SELECT * FROM "PatientIdentifiers" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "PatientId"=@patientId AND "Namespace"='local-mrn'
            """,new{facilityId,patientId},ct:ct);
        if(existing is not null)return existing;
        var sequence=await store.QueryFirstOrDefaultAsync<long?>("""
            INSERT INTO "PatientIdentifierCounters" ("Id","TenantId","FacilityId","Namespace","CurrentValue","CreatedAt","UpdatedAt","CreatedBy","UpdatedBy","IsActive","IsDeleted")
            VALUES (@id,@TenantId,@facilityId,'local-mrn',1,CURRENT_TIMESTAMP,CURRENT_TIMESTAMP,@actor,@actor,true,false)
            ON CONFLICT ("TenantId","FacilityId","Namespace") DO UPDATE SET "CurrentValue"="PatientIdentifierCounters"."CurrentValue"+1,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"=@actor
            WHERE "PatientIdentifierCounters"."CurrentValue"<9223372036854775807
            RETURNING "CurrentValue"
            """,new{id=Guid.NewGuid(),facilityId,actor=tenant.UserId},ct:ct);
        if(!sequence.HasValue)throw new DomainException("Identifier namespace is exhausted; operator review required.",409);
        var identifier=new PatientIdentifier{FacilityId=facilityId,PatientId=patientId,Sequence=sequence.Value,Value="MRN-"+sequence.Value.ToString("D10",System.Globalization.CultureInfo.InvariantCulture)};
        await store.InsertAsync(identifier,ct:ct);
        await audit.RecordAsync(new(patientId,"PatientIdentifier",identifier.Id,"allocate","committed",Purpose:"registration"),ct,store);
        return identifier;
    }
}