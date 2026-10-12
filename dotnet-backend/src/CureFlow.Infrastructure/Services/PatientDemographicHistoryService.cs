using CureFlow.Infrastructure.Identity;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientDemographicHistoryService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,IClinicalAccessAuditService audit):IPatientDemographicHistoryService
{
    public async Task<IReadOnlyList<PatientDemographicHistoryEntry>> GetAsync(Guid patientId,long? beforeRevision,int limit,CancellationToken ct)
    {
        if(limit is <1 or >100||beforeRevision is <=0)throw new ValidationException("Supply a bounded history page and positive revision cursor.");
        IReadOnlyList<PatientDemographicHistoryEntry> result=[];
        await db.TransactionAsync(async store=>{
            if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
            var actor=await store.QueryFirstOrDefaultAsync<User>("""
                SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                """,new{Id=tenant.UserId},ct:ct);
            if(actor is null||!(await permissions.GetPermissionCodesAsync(actor,ct,store)).Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal))throw new ForbiddenException();
            _=await store.GetByIdAsync<Patient>(patientId,ct:ct)??throw new NotFoundException("Patient");
            var rows=await store.QueryAsync<PatientDemographicVersion>("""
                SELECT * FROM "PatientDemographicVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
                  AND (@beforeRevision::bigint IS NULL OR "Revision"<@beforeRevision) ORDER BY "Revision" DESC LIMIT @limit
                """,new{patientId,beforeRevision,limit},ct:ct);
            await audit.RecordAsync(new(patientId,"PatientDemographicHistory",patientId,"read","authorized",beforeRevision?.ToString(),"registration"),ct,store);
            result=rows.Select(r=>new PatientDemographicHistoryEntry(r.Id,r.Revision,r.CreatedAt,r.CreatedBy,r.SourceType,r.SourceReference,r.Reason,r.VerificationStatus,r.BeforeValue,r.AfterValue)).ToArray();
        },ct);
        return result;
    }
}
