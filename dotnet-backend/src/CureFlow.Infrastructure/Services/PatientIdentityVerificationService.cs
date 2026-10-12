using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientIdentityVerificationService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IPatientIdentifierService identifiers,IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IPatientIdentityVerificationService
{
    private async Task RequireAsync(ICureFlowDbSession store,string capability,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct);
        if(actor is null)throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains(capability,StringComparer.Ordinal)||(capability!=CureFlowPermissions.PatientCreate&&!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal)))
            throw new ForbiddenException();
    }
    private TimeSpan Horizon(string section)
    {
        var hours=config.GetValue<int?>($"{section}:ApprovedRetryHorizonHours");
        if(hours is not >0 or >8760)throw new DomainException("The identity command retry policy is not configured.",503);
        return TimeSpan.FromHours(hours.Value);
    }
    private static void Text(string? text,int length,string field)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>length||text.Any(char.IsControl))throw new ValidationException($"Supply a bounded {field}.");
    }
    public Task<IdempotentCommandResult> RegisterUnknownAsync(UnknownPatientRegistrationInput input,string key,CancellationToken ct)
    {
        Text(input.Reason,500,"intake reason");
        if(input.FacilityId==Guid.Empty||input.SourceReference is not null&&(input.SourceReference.Length>200||input.SourceReference.Any(char.IsControl)))
            throw new ValidationException("Supply an explicit facility and bounded source reference.");
        return commands.ExecuteAsync("patient.identity.unknown-intake",key,input,Horizon("UnknownPatientRegistration"),
            (store,token)=>RequireAsync(store,CureFlowPermissions.PatientCreate,token),async(store,token)=>{
                _=await store.QueryFirstOrDefaultAsync<CureFlow.Domain.Entities.Saas.Facility>("""
                    SELECT * FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "Id"=public.cureflow_lock_active_facility(@TenantId,@Id)
                    """,new{Id=input.FacilityId},ct:token)??throw new NotFoundException("Active facility");
                var patient=new Patient{IdentityStatus="unknown",Phone="",Name="",BirthDatePrecision="unknown"};
                patient.Name=$"Unidentified {patient.Id:N}";
                await store.ExecuteAsync("""
                    SELECT set_config('app.demographic_source_type','staff_observed',true),
                      set_config('app.demographic_source_reference',@reference,true),set_config('app.demographic_reason',@reason,true)
                    """,new{reference=input.SourceReference?.Trim()??"",reason=input.Reason.Trim()},ct:token);
                await store.InsertAsync(patient,ct:token);
                await store.InsertAsync(new TemporaryPatientIdentity{PatientId=patient.Id,FacilityId=input.FacilityId,
                    Value=$"TEMP-{patient.Id:N}",Reason=input.Reason.Trim(),SourceReference=input.SourceReference?.Trim()},ct:token);
                await identifiers.AllocateLocalMrnAsync(store,input.FacilityId,patient.Id,token);
                await audit.RecordAsync(new(patient.Id,"TemporaryPatientIdentity",patient.Id,"register","committed",null,"registration"),token,store);
                return(new CommandReceipt(patient.Id,null,CommandOutcome.Created),201);
            },ct);
    }
    public Task<IdempotentCommandResult> VerifyAsync(Guid patientId,PatientIdentityVerificationInput input,string key,CancellationToken ct)
    {
        Text(input.ProofReference,200,"proof reference");Text(input.Reason,500,"verification reason");
        PatientConcurrency.RequireTag(patientId,new[]{input.ExpectedPatientTag});
        if(input.DemographicRevision<=0)throw new ValidationException("Supply the current demographic revision.");
        var policy=config["PatientIdentityVerification:ApprovedPolicyVersion"];
        if(string.IsNullOrWhiteSpace(policy)||policy.Length>100||policy.Any(char.IsControl))throw new DomainException("Accountable local identity verification policy is not configured.",503);
        return commands.ExecuteAsync("patient.identity.verify",key,new{patientId,input},Horizon("PatientIdentityVerification"),
            (store,token)=>RequireAsync(store,CureFlowPermissions.PatientIdentityVerify,token),async(store,token)=>{
                var patient=await store.QueryFirstOrDefaultAsync<Patient>("""
                    SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=@patientId AND "IsActive" AND NOT "IsDeleted" FOR UPDATE
                    """,new{patientId},ct:token)??throw new NotFoundException("Active patient");
                if(PatientConcurrency.Tag(patient)!=input.ExpectedPatientTag)throw new DomainException("Patient evidence changed; refresh before verification.",412);
                if(string.IsNullOrWhiteSpace(patient.Name)||patient.Name.StartsWith("Unidentified ",StringComparison.Ordinal))
                    throw new ConflictException("Record reviewed nonplaceholder local demographics before verification.");
                var source=await store.QueryFirstOrDefaultAsync<PatientDemographicVersion>("""
                    SELECT * FROM "PatientDemographicVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId ORDER BY "Revision" DESC LIMIT 1
                    """,new{patientId},ct:token)??throw new ConflictException("Record demographic source evidence before verification.");
                if(source.Revision!=input.DemographicRevision)throw new DomainException("Demographic evidence changed; reload its current revision.",412);
                await store.ExecuteAsync("""
                    UPDATE "Patients" SET "IdentityStatus"='verified_local',"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"=@actor
                      WHERE "TenantId"=@TenantId AND "Id"=@patientId
                    """,new{patientId,actor=tenant.UserId},ct:token);
                var persisted=await store.GetByIdAsync<Patient>(patientId,ct:token)??throw new NotFoundException("Patient");
                var evidence=new PatientIdentityVerification{PatientId=patientId,DemographicVersionId=source.Id,ProofReference=input.ProofReference.Trim(),
                    Reason=input.Reason.Trim(),PolicyVersion=policy,PatientTag=PatientConcurrency.Tag(persisted)};
                await store.InsertAsync(evidence,ct:token);
                await audit.RecordAsync(new(patientId,"PatientIdentityVerification",evidence.Id,"verify-local","committed",source.Revision.ToString(),"registration"),token,store);
                // This local review never links accounts, merges charts, proves guardianship, or releases publications.
                return(new CommandReceipt(patientId,source.Revision,CommandOutcome.Updated),200);
            },ct);
    }
    public async Task<PatientIdentityStatusView> GetAsync(Guid patientId,CancellationToken ct)
    {
        PatientIdentityStatusView? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,CureFlowPermissions.PatientView,ct);
            var patient=await store.QueryFirstOrDefaultAsync<Patient>("""
                SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=@patientId AND NOT "IsDeleted" FOR SHARE
                """,new{patientId},ct:ct)??throw new NotFoundException("Patient");
            var temporary=await store.QueryFirstOrDefaultAsync<TemporaryPatientIdentity>("""
                SELECT * FROM "TemporaryPatientIdentities" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId
                """,new{patientId},ct:ct);
            var latest=await store.QueryFirstOrDefaultAsync<PatientDemographicVersion>("""
                SELECT * FROM "PatientDemographicVersions" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId ORDER BY "Revision" DESC LIMIT 1
                """,new{patientId},ct:ct);
            var verification=await store.QueryFirstOrDefaultAsync<PatientIdentityVerification>("""
                SELECT * FROM "PatientIdentityVerifications" WHERE "TenantId"=@TenantId AND "PatientId"=@patientId ORDER BY "CreatedAt" DESC,"Id" DESC LIMIT 1
                """,new{patientId},ct:ct);
            var current=patient.IdentityStatus=="verified_local"&&latest is not null&&verification is not null&&verification.DemographicVersionId==latest.Id&&verification.PatientTag==PatientConcurrency.Tag(patient);
            result=new(patientId,patient.IdentityStatus,current,temporary?.Value,temporary?.FacilityId,latest?.Revision);
            await audit.RecordAsync(new(patientId,"PatientIdentityVerification",patientId,"read","authorized",latest?.Revision.ToString(),"registration"),ct,store);
        },ct);
        return result!;
    }
}
