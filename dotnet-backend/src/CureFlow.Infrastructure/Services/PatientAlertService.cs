using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientAlertService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IPatientAlertService
{
    private async Task RequireAsync(ICureFlowDbSession store,string capability,bool confidentiality,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{Id=tenant.UserId},ct:ct);
        if(actor is null)throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains(capability,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)
            ||confidentiality&&!codes.Contains(CureFlowPermissions.PatientRestrictedView,StringComparer.Ordinal))throw new ForbiddenException();
    }
    private static PatientAlertView View(PatientAlertVersion r)=>new(r.AlertId,r.Revision,r.Key,r.Category,r.Title,r.Detail,r.Severity,r.AlertIsActive,
        r.SourceType,r.SourceReference,r.Reason,r.ReportedDeathDate,r.EffectiveFrom,r.CreatedBy);
    public async Task<IReadOnlyList<PatientAlertView>> ListAsync(Guid patientId,CancellationToken ct)
    {
        IReadOnlyList<PatientAlertView> result=[];
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,CureFlowPermissions.PatientAlertsView,false,ct);
            _=await store.GetByIdAsync<Patient>(patientId,ct:ct)??throw new NotFoundException("Patient");
            var rows=await store.QueryAsync<PatientAlertVersion>("""
                SELECT DISTINCT ON ("Category","Key") * FROM "PatientAlertVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
                  ORDER BY "Category","Key","Revision" DESC LIMIT 100
                """,new{patientId},ct:ct);
            result=rows.Select(View).ToArray();
            await audit.RecordAsync(new(patientId,"PatientAlerts",patientId,"read","authorized",null,"care"),ct,store);
        },ct);
        return result;
    }
    public async Task<IReadOnlyList<PatientAlertView>> HistoryAsync(Guid patientId,Guid alertId,long? beforeRevision,CancellationToken ct)
    {
        if(beforeRevision is <=0)throw new ValidationException("Supply a positive history revision.");
        IReadOnlyList<PatientAlertView> result=[];
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,CureFlowPermissions.PatientAlertsView,false,ct);
            _=await store.GetByIdAsync<Patient>(patientId,ct:ct)??throw new NotFoundException("Patient");
            var rows=await store.QueryAsync<PatientAlertVersion>("""
                SELECT * FROM "PatientAlertVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId AND "AlertId"=@alertId
                  AND (@beforeRevision::bigint IS NULL OR "Revision"<@beforeRevision) ORDER BY "Revision" DESC LIMIT 100
                """,new{patientId,alertId,beforeRevision},ct:ct);
            result=rows.Select(View).ToArray();
            await audit.RecordAsync(new(patientId,"PatientAlerts",alertId,"history","authorized",beforeRevision?.ToString(),"care"),ct,store);
        },ct);
        return result;
    }
    public Task<IdempotentCommandResult> SaveAsync(Guid patientId,Guid? alertId,PatientAlertInput input,string key,CancellationToken ct)
    {
        if(patientId==Guid.Empty||alertId==Guid.Empty||input.Category is not ("identity" or "clinical" or "confidentiality" or "deceased_reported")
           ||string.IsNullOrWhiteSpace(input.Key)||input.Key.Length>80||input.Key.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-' and not '.')
           ||input.Category=="confidentiality"&&input.Key!="chart"||input.Category=="deceased_reported"&&input.Key!="status"
           ||string.IsNullOrWhiteSpace(input.Title)||input.Title.Length>120||input.Title.Any(char.IsControl)
           ||input.Detail is not null&&(input.Detail.Length>1000||input.Detail.Any(c=>char.IsControl(c)&&c is not '\r' and not '\n' and not '\t'))
           ||input.Severity is not ("info" or "warning" or "critical")||input.Source is null
           ||input.ReportedDeathDate.HasValue&&(input.Category!="deceased_reported"||input.ReportedDeathDate.Value>DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new ValidationException("Supply a bounded alert classification, content and source evidence.");
        PatientDemographicRules.ValidateSource(input.Source);
        if(alertId.HasValue&&input.ExpectedRevision is not >0)throw new DomainException("Supply the current alert revision.",428);
        if(!alertId.HasValue&&input.ExpectedRevision.HasValue)throw new ValidationException("New alerts cannot carry a prior revision.");
        var hours=config.GetValue<int?>("PatientAlerts:ApprovedRetryHorizonHours");
        var policy=config["PatientAlerts:ApprovedPolicyVersion"];
        if(hours is not >0 or >8760||string.IsNullOrWhiteSpace(policy)||policy.Length>100||policy.Any(char.IsControl))
            throw new DomainException("The reviewed alert/confidentiality policy is not configured.",503);
        return commands.ExecuteAsync("patient.alert.save",key,new{patientId,alertId,input},TimeSpan.FromHours(hours.Value),
            async(store,token)=>{
                await RequireAsync(store,CureFlowPermissions.PatientAlertsManage,input.Category=="confidentiality",token);
                _=await store.GetByIdAsync<Patient>(patientId,ct:token)??throw new NotFoundException("Patient");
            },
            async(store,token)=>{
                _=await store.QueryFirstOrDefaultAsync<Patient>("""
                    SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=@patientId AND "IsActive" AND NOT "IsDeleted" FOR UPDATE
                    """,new{patientId},ct:token)??throw new NotFoundException("Active patient");
                var previous=await store.QueryFirstOrDefaultAsync<PatientAlertVersion>("""
                    SELECT * FROM "PatientAlertVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId AND "Category"=@Category AND "Key"=@Key
                      ORDER BY "Revision" DESC LIMIT 1
                    """,new{patientId,input.Category,input.Key},ct:token);
                if(alertId.HasValue&&(previous is null||previous.AlertId!=alertId.Value))throw new NotFoundException("Patient alert");
                if(!alertId.HasValue&&previous is not null)throw new ConflictException("This alert key already exists; use its current revision.");
                if(previous is not null&&previous.Revision!=input.ExpectedRevision)throw new DomainException("Alert changed; reload before editing.",412);
                if(previous is null)
                {
                    var count=await store.QuerySingleAsync<int>("""
                        SELECT COUNT(*)::integer FROM (SELECT DISTINCT "Category","Key" FROM "PatientAlertVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId) heads
                        """,new{patientId},ct:token);
                    if(count>=100)throw new ConflictException("Reconcile existing patient alerts before adding another.");
                }
                if(previous?.Revision==long.MaxValue)throw new ConflictException("Alert revision requires reviewed maintenance.");
                var row=new PatientAlertVersion{AlertId=alertId??Guid.NewGuid(),PatientId=patientId,Key=input.Key,Category=input.Category,Revision=checked((previous?.Revision??0)+1),
                    Title=input.Title.Trim(),Detail=input.Detail?.Trim(),Severity=input.Severity,AlertIsActive=input.Active,
                    SourceType=input.Source.SourceType,SourceReference=input.Source.SourceReference?.Trim(),Reason=input.Source.Reason.Trim(),
                    ReportedDeathDate=input.ReportedDeathDate,EffectiveFrom=DateTime.UtcNow,PolicyVersion=policy};
                await store.InsertAsync(row,ct:token);
                if(input.Category=="confidentiality")
                {
                    var state=await store.QueryFirstOrDefaultAsync<PatientConfidentialityState>("""
                        SELECT * FROM "PatientConfidentialityStates" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId FOR UPDATE
                        """,new{patientId},ct:token);
                    if(state is null)await store.InsertAsync(new PatientConfidentialityState{PatientId=patientId,Restricted=input.Active,Revision=row.Revision},ct:token);
                    else await store.ExecuteAsync("""
                        UPDATE "PatientConfidentialityStates" SET "Restricted"=@Active,"Revision"=@Revision,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"=@Actor
                         WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
                        """,new{patientId,input.Active,row.Revision,Actor=tenant.UserId},ct:token);
                }
                await audit.RecordAsync(new(patientId,"PatientAlerts",row.AlertId,previous is null?"create":"correct","committed",row.Revision.ToString(),"care"),token,store);
                // Death is reported evidence, never a legal certificate or family-access grant.
                return(new CommandReceipt(row.AlertId,row.Revision,previous is null?CommandOutcome.Created:CommandOutcome.Updated),previous is null?201:200);
            },ct);
    }
}
