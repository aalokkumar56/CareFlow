using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientIdentityReviewService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IPatientIdentityReviewService
{
    private async Task RequireAsync(ICureFlowDbSession store,string capability,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct);
        if(actor is null)throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains(capability,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal))throw new ForbiddenException();
    }
    public async Task<IdentityReviewView> GetAsync(Guid reviewId,CancellationToken ct)
    {
        await RequireAsync(db,CureFlowPermissions.IdentityReviewView,ct);
        var row=await db.QueryFirstOrDefaultAsync<PatientIdentityReviewVersion>("""
            SELECT * FROM "PatientIdentityReviewVersions" WHERE "TenantId"=@TenantId AND "ReviewId"=@reviewId ORDER BY "Revision" DESC LIMIT 1
            """,new{reviewId},ct:ct)??throw new NotFoundException("Identity review");
        var first=await db.GetByIdAsync<Patient>(row.FirstPatientId,ct:ct)??throw new NotFoundException("Patient");
        var second=await db.GetByIdAsync<Patient>(row.SecondPatientId,ct:ct)??throw new NotFoundException("Patient");
        await AuditAsync(db,row,"read","authorized",ct);
        return new(row.ReviewId,row.Revision,row.Decision,row.EvidenceReference,row.Reason,row.ExactNameSignal,row.SharedPhoneSignal,
            new(first.Id,first.Name,first.Phone,row.FirstPatientTag,PatientConcurrency.Tag(first)),new(second.Id,second.Name,second.Phone,row.SecondPatientTag,PatientConcurrency.Tag(second)),
            row.FirstPatientTag==PatientConcurrency.Tag(first)&&row.SecondPatientTag==PatientConcurrency.Tag(second));
    }
    public Task<IdempotentCommandResult> SaveAsync(Guid? reviewId,IdentityReviewInput input,string key,CancellationToken ct)
    {
        if(input.FirstPatientId==Guid.Empty||input.SecondPatientId==Guid.Empty||input.FirstPatientId==input.SecondPatientId||reviewId==Guid.Empty
           ||input.Decision is not ("open" or "different" or "potential_same" or "inconclusive")
           ||string.IsNullOrWhiteSpace(input.EvidenceReference)||input.EvidenceReference.Length>200||input.EvidenceReference.Any(char.IsControl)
           ||string.IsNullOrWhiteSpace(input.Reason)||input.Reason.Length>500||input.Reason.Any(char.IsControl))throw new ValidationException("Supply two distinct local patient identities and bounded review evidence.");
        PatientConcurrency.RequireTag(input.FirstPatientId,new[]{input.FirstPatientTag});
        PatientConcurrency.RequireTag(input.SecondPatientId,new[]{input.SecondPatientTag});
        if(reviewId.HasValue&&input.ExpectedRevision is not >0)throw new DomainException("Supply current review revision.",428);
        if(!reviewId.HasValue&&(input.ExpectedRevision.HasValue||input.Decision!="open"))throw new ValidationException("New reviews begin open without a prior revision.");
        var horizon=config.GetValue<int?>("IdentityReviews:ApprovedRetryHorizonHours");
        if(horizon is not >0 or >8760)throw new DomainException("Identity review retry policy is not configured.",503);
        return commands.ExecuteAsync("patient.identity-review.save",key,new{reviewId,input},TimeSpan.FromHours(horizon.Value),
            (store,token)=>RequireAsync(store,CureFlowPermissions.IdentityReviewManage,token),async(store,token)=>{
                var ids=new[]{input.FirstPatientId,input.SecondPatientId}.OrderBy(id=>id.ToString("N"),StringComparer.Ordinal).ToArray();
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=$"identity-review:{tenant.TenantId:D}:{ids[0]:D}:{ids[1]:D}"},ct:token);
                var patients=await store.QueryAsync<Patient>("""
                    SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=ANY(@ids) AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE
                    """,new{ids},ct:token);
                if(patients.Count!=2)throw new NotFoundException("Active patient pair");
                var first=patients.Single(p=>p.Id==ids[0]);var second=patients.Single(p=>p.Id==ids[1]);
                var firstTag=input.FirstPatientId==first.Id?input.FirstPatientTag:input.SecondPatientTag;
                var secondTag=input.FirstPatientId==second.Id?input.FirstPatientTag:input.SecondPatientTag;
                if(PatientConcurrency.Tag(first)!=firstTag||PatientConcurrency.Tag(second)!=secondTag)throw new DomainException("Patient evidence changed; refresh both records before review.",412);
                var prior=await store.QueryFirstOrDefaultAsync<PatientIdentityReviewVersion>("""
                    SELECT * FROM "PatientIdentityReviewVersions" WHERE "TenantId"=@TenantId AND "FirstPatientId"=@first AND "SecondPatientId"=@second ORDER BY "Revision" DESC LIMIT 1
                    """,new{first=first.Id,second=second.Id},ct:token);
                if(reviewId.HasValue&&(prior is null||prior.ReviewId!=reviewId.Value))throw new NotFoundException("Identity review");
                if(!reviewId.HasValue&&prior is not null)throw new DomainException("This pair already has a review; use its current revision.",409);
                if(prior is not null&&prior.Revision!=input.ExpectedRevision)throw new DomainException("Review changed; reload.",412);
                var row=new PatientIdentityReviewVersion{ReviewId=reviewId??Guid.NewGuid(),FirstPatientId=first.Id,SecondPatientId=second.Id,Revision=checked((prior?.Revision??0)+1),
                    Decision=input.Decision,EvidenceReference=input.EvidenceReference.Trim(),Reason=input.Reason.Trim(),FirstPatientTag=firstTag,SecondPatientTag=secondTag,
                    ExactNameSignal=StringComparer.OrdinalIgnoreCase.Equals(first.Name.Trim(),second.Name.Trim()),SharedPhoneSignal=!string.IsNullOrWhiteSpace(first.Phone)&&first.Phone==second.Phone};
                // Equality signals are not identity proof. No merge, alias, clinical reassignment or sharing operation is called.
                await store.InsertAsync(row,ct:token);
                await AuditAsync(store,row,prior is null?"create":"decision","committed",token);
                return(new CommandReceipt(row.ReviewId,row.Revision,prior is null?CommandOutcome.Created:CommandOutcome.Updated),prior is null?201:200);
            },ct);
    }
    private async Task AuditAsync(ICureFlowDbSession store,PatientIdentityReviewVersion row,string action,string outcome,CancellationToken ct)
    {
        await audit.RecordAsync(new(row.FirstPatientId,"IdentityReview",row.ReviewId,action,outcome,row.Revision.ToString(),"registration"),ct,store);
        await audit.RecordAsync(new(row.SecondPatientId,"IdentityReview",row.ReviewId,action,outcome,row.Revision.ToString(),"registration"),ct,store);
    }
}