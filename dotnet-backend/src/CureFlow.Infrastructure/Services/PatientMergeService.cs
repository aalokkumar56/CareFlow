using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
namespace CureFlow.Infrastructure.Services;
public sealed partial class PatientMergeService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IPatientMergeService
{
    private string Scope=>$"patient-mapping:{tenant.TenantId:D}";
    private async Task RequireAsync(ICureFlowDbSession store,string capability,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.TenantId==Guid.Empty||!tenant.UserId.HasValue)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{Id=tenant.UserId},ct:ct);
        if(actor is null)throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains(capability,StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal))throw new ForbiddenException();
    }
    private (TimeSpan Horizon,string Policy) Policy()
    {
        var horizon=config.GetValue<int?>("PatientMappings:ApprovedRetryHorizonHours");
        var policy=config["PatientMappings:ApprovedPolicyVersion"];
        if(horizon is not >0 or >8760||string.IsNullOrWhiteSpace(policy)||policy.Length>100||policy.Any(char.IsControl))
            throw new DomainException("Reviewed patient mapping policy is not configured.",503);
        return(TimeSpan.FromHours(horizon.Value),policy);
    }
    private static void Reason(string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>500||reason.Any(char.IsControl))throw new ValidationException("Supply a bounded mapping reason.");
    }
    public async Task<PatientResolution> ResolveAsync(Guid patientId,CancellationToken ct)
    {
        if(patientId==Guid.Empty)throw new ValidationException("Supply a local patient identity.");
        PatientResolution? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,CureFlowPermissions.IdentityMergeView,ct);
            await store.ExecuteAsync("SELECT pg_advisory_xact_lock_shared(hashtextextended(@scope,0))",new{scope=Scope},ct:ct);
            var patient=await store.GetByIdAsync<Patient>(patientId,ct:ct);
            if(patient is null||patient.IsDeleted)throw new NotFoundException("Patient");
            result=await ResolveCore(store,patientId,ct);
            await audit.RecordAsync(new(patientId,"PatientIdentityMapping",patientId,"resolve","authorized",null,"registration"),ct,store);
        },ct);
        return result!;
    }
    private async Task<PatientResolution> ResolveCore(ICureFlowDbSession store,Guid patientId,CancellationToken ct)
    {
        var visited=new HashSet<Guid>();
        var mappings=new List<Guid>();
        var current=patientId;
        var hold=false;
        for(var depth=0;depth<32;depth++)
        {
            if(!visited.Add(current))throw new ConflictException("Identity mapping requires correction review.");
            hold|=await store.QuerySingleAsync<bool>("""
                SELECT EXISTS(SELECT 1 FROM "PatientLinkJournals" j JOIN "PatientMerges" m
                  ON j."TenantId"=m."TenantId" AND j."MergeId"=m."Id"
                  WHERE m."TenantId"=@TenantId AND (m."SourcePatientId"=@Id OR m."SurvivorPatientId"=@Id) AND j."PublicationHold")
                """,new{Id=current},ct:ct);
            var edge=await store.QueryFirstOrDefaultAsync<PatientMerge>("""
                SELECT * FROM "PatientMerges" WHERE "TenantId"=@TenantId AND "SourcePatientId"=@Id AND "ReversedAt" IS NULL
                """,new{Id=current},ct:ct);
            if(edge is null)return new(patientId,current,mappings,hold);
            mappings.Add(edge.Id);current=edge.SurvivorPatientId;
        }
        throw new ConflictException("Identity mapping exceeds the safe resolution depth; review the mappings.");
    }
    public Task<IdempotentCommandResult> MergeAsync(Guid survivorId,PatientMergeInput input,string key,CancellationToken ct)
    {
        Reason(input.Reason);
        if(survivorId==Guid.Empty||input.DuplicatePatientId==Guid.Empty||input.DuplicatePatientId==survivorId||input.ReviewId==Guid.Empty||input.ReviewRevision<=0)
            throw new ValidationException("Supply distinct local patients and the current reviewed pair.");
        PatientConcurrency.RequireTag(input.DuplicatePatientId,new[]{input.SourcePatientTag});
        PatientConcurrency.RequireTag(survivorId,new[]{input.SurvivorPatientTag});
        var policy=Policy();
        return commands.ExecuteAsync("patient.identity.merge",key,new{survivorId,input},policy.Horizon,
            (store,token)=>RequireAsync(store,CureFlowPermissions.IdentityMergeManage,token),
            async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=Scope},ct:token);
                var ids=new[]{survivorId,input.DuplicatePatientId}.OrderBy(id=>id.ToString("N"),StringComparer.Ordinal).ToArray();
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=$"identity-review:{tenant.TenantId:D}:{ids[0]:D}:{ids[1]:D}"},ct:token);
                var patients=await store.QueryAsync<Patient>("""
                    SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=ANY(@ids)
                      AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE
                    """,new{ids},ct:token);
                if(patients.Count!=2)throw new NotFoundException("Active local patient pair");
                var source=patients.Single(p=>p.Id==input.DuplicatePatientId);
                var survivor=patients.Single(p=>p.Id==survivorId);
                if(PatientConcurrency.Tag(source)!=input.SourcePatientTag||PatientConcurrency.Tag(survivor)!=input.SurvivorPatientTag)
                    throw new DomainException("Patient evidence changed; refresh both records.",412);
                var review=await store.QueryFirstOrDefaultAsync<PatientIdentityReviewVersion>("""
                    SELECT * FROM "PatientIdentityReviewVersions" WHERE "TenantId"=@TenantId AND "ReviewId"=@Id
                      ORDER BY "Revision" DESC LIMIT 1
                    """,new{Id=input.ReviewId},ct:token)??throw new NotFoundException("Identity review");
                if(review.Revision!=input.ReviewRevision)throw new DomainException("Identity review changed; refresh it.",412);
                if(review.Decision!="potential_same"||!ids.Contains(review.FirstPatientId)||!ids.Contains(review.SecondPatientId)
                    ||review.CreatedBy is null||review.CreatedBy==tenant.UserId)
                    throw new ConflictException("A distinct reviewer must first record current evidence for this pair.");
                var reviewedSource=source.Id==review.FirstPatientId?review.FirstPatientTag:review.SecondPatientTag;
                var reviewedSurvivor=survivor.Id==review.FirstPatientId?review.FirstPatientTag:review.SecondPatientTag;
                if(reviewedSource!=input.SourcePatientTag||reviewedSurvivor!=input.SurvivorPatientTag)
                    throw new DomainException("Reviewed patient evidence is stale.",412);
                var reviewer=await store.QueryFirstOrDefaultAsync<User>("""
                    SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                    """,new{Id=review.CreatedBy},ct:token);
                if(reviewer is null||!(await permissions.GetPermissionCodesAsync(reviewer,token,store)).Contains(CureFlowPermissions.IdentityReviewManage,StringComparer.Ordinal))
                    throw new ForbiddenException("The evidence reviewer no longer has review authority.");
                var sourceResolution=await ResolveCore(store,source.Id,token);
                var targetResolution=await ResolveCore(store,survivor.Id,token);
                // Only explicit canonical endpoints: never redirect a caller to an inferred chart.
                if(sourceResolution.CanonicalPatientId!=source.Id||targetResolution.CanonicalPatientId!=survivor.Id
                    ||sourceResolution.RequiresPublicationReview||targetResolution.RequiresPublicationReview)
                    throw new ConflictException("Resolve existing mapping or correction holds before another merge.");
                var ancestors=await store.QuerySingleAsync<int>("""
                    WITH RECURSIVE ancestors(id,depth) AS (
                      SELECT @Id::uuid,0 UNION ALL SELECT m."SourcePatientId",a.depth+1 FROM "PatientMerges" m JOIN ancestors a ON m."SurvivorPatientId"=a.id
                       WHERE m."TenantId"=@TenantId AND m."ReversedAt" IS NULL AND a.depth<32)
                    SELECT COALESCE(MAX(depth),0) FROM ancestors
                    """,new{Id=source.Id},ct:token);
                if(ancestors>=31)throw new ConflictException("Mapping would exceed the safe resolution depth.");
                var row=new PatientMerge{SourcePatientId=source.Id,SurvivorPatientId=survivor.Id,ReviewVersionId=review.Id,
                    SourcePatientTag=input.SourcePatientTag,SurvivorPatientTag=input.SurvivorPatientTag,
                    Reason=input.Reason.Trim(),PolicyVersion=policy.Policy,MergedAt=DateTime.UtcNow};
                await store.InsertAsync(row,ct:token);
                await store.InsertAsync(new PatientLinkJournal{MergeId=row.Id,EntityId=source.Id,AppliedAt=row.MergedAt,
                    BeforeValue=JsonSerializer.Serialize(new{canonicalPatientId=source.Id}),
                    AfterValue=JsonSerializer.Serialize(new{canonicalPatientId=survivor.Id,reviewVersionId=review.Id})},ct:token);
                await AuditAsync(store,row,"merge",token);
                // Original Patients/clinical/financial rows and their foreign keys are deliberately unchanged.
                return(new CommandReceipt(row.Id,row.Revision,CommandOutcome.Created),201);
            },ct);
    }
    private async Task AuditAsync(ICureFlowDbSession store,PatientMerge row,string action,CancellationToken ct)
    {
        await audit.RecordAsync(new(row.SourcePatientId,"PatientIdentityMapping",row.Id,action,"committed",row.Revision.ToString(),"registration"),ct,store);
        await audit.RecordAsync(new(row.SurvivorPatientId,"PatientIdentityMapping",row.Id,action,"committed",row.Revision.ToString(),"registration"),ct,store);
    }
    public Task<IdempotentCommandResult> ReverseAsync(Guid mappingId,PatientMappingReversalInput input,string key,CancellationToken ct)
    {
        Reason(input.Reason);
        if(mappingId==Guid.Empty||input.ReviewId==Guid.Empty||input.ReviewRevision<=0||input.ExpectedMappingRevision<=0)
            throw new ValidationException("Supply current mapping and correction review revisions.");
        var policy=Policy();
        return commands.ExecuteAsync("patient.identity.reverse",key,new{mappingId,input},policy.Horizon,
            (store,token)=>RequireAsync(store,CureFlowPermissions.IdentityMergeReverse,token),async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=Scope},ct:token);
                var mapping=await store.QueryFirstOrDefaultAsync<PatientMerge>("""
                    SELECT * FROM "PatientMerges" WHERE "TenantId"=@TenantId AND "Id"=@mappingId FOR UPDATE
                    """,new{mappingId},ct:token)??throw new NotFoundException("Identity mapping");
                if(mapping.Revision!=input.ExpectedMappingRevision)throw new DomainException("Mapping changed; refresh it.",412);
                if(mapping.ReversedAt.HasValue)throw new ConflictException("This mapping was already reversed.");
                var ids=new[]{mapping.SourcePatientId,mapping.SurvivorPatientId}.OrderBy(id=>id.ToString("N"),StringComparer.Ordinal).ToArray();
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@scope,0))",new{scope=$"identity-review:{tenant.TenantId:D}:{ids[0]:D}:{ids[1]:D}"},ct:token);
                PatientConcurrency.RequireTag(mapping.SourcePatientId,new[]{input.SourcePatientTag});
                PatientConcurrency.RequireTag(mapping.SurvivorPatientId,new[]{input.SurvivorPatientTag});
                var patients=await store.QueryAsync<Patient>("""
                    SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND "Id"=ANY(@ids)
                      AND "IsActive" AND NOT "IsDeleted" ORDER BY "Id" FOR SHARE
                    """,new{ids},ct:token);
                if(patients.Count!=2)throw new NotFoundException("Active local patient pair");
                var source=patients.Single(p=>p.Id==mapping.SourcePatientId);var survivor=patients.Single(p=>p.Id==mapping.SurvivorPatientId);
                if(PatientConcurrency.Tag(source)!=input.SourcePatientTag||PatientConcurrency.Tag(survivor)!=input.SurvivorPatientTag)
                    throw new DomainException("Patient evidence changed; refresh both records.",412);
                var review=await store.QueryFirstOrDefaultAsync<PatientIdentityReviewVersion>("""
                    SELECT * FROM "PatientIdentityReviewVersions" WHERE "TenantId"=@TenantId AND "ReviewId"=@Id ORDER BY "Revision" DESC LIMIT 1
                    """,new{Id=input.ReviewId},ct:token)??throw new NotFoundException("Identity review");
                if(review.Revision!=input.ReviewRevision)throw new DomainException("Correction review changed; refresh it.",412);
                if(review.Decision!="different"||review.CreatedAt<=mapping.MergedAt||review.CreatedBy is null||review.CreatedBy==tenant.UserId
                   ||!ids.Contains(review.FirstPatientId)||!ids.Contains(review.SecondPatientId))
                    throw new ConflictException("A distinct reviewer must record a subsequent different-identities decision; ambiguous correction requires review.");
                if((source.Id==review.FirstPatientId?review.FirstPatientTag:review.SecondPatientTag)!=input.SourcePatientTag
                   ||(survivor.Id==review.FirstPatientId?review.FirstPatientTag:review.SecondPatientTag)!=input.SurvivorPatientTag)
                    throw new DomainException("Correction evidence is stale.",412);
                var reviewer=await store.QueryFirstOrDefaultAsync<User>("""
                    SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@Id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
                    """,new{Id=review.CreatedBy},ct:token);
                if(reviewer is null||!(await permissions.GetPermissionCodesAsync(reviewer,token,store)).Contains(CureFlowPermissions.IdentityReviewManage,StringComparer.Ordinal))
                    throw new ForbiddenException("The correction reviewer no longer has review authority.");
                var dependent=await store.QuerySingleAsync<bool>("""
                    SELECT EXISTS(SELECT 1 FROM "PatientMerges" WHERE "TenantId"=@TenantId AND "Id"<>@mappingId AND "ReversedAt" IS NULL
                      AND ("SourcePatientId"=ANY(@ids) OR "SurvivorPatientId"=ANY(@ids)))
                    """,new{mappingId,ids},ct:token);
                if(dependent)throw new ConflictException("Dependent mappings require a reviewed reconciliation plan before reversal.");
                var changed=await store.ExecuteAsync("""
                    UPDATE "PatientMerges" SET "ReversedAt"=CURRENT_TIMESTAMP,"ReversalReason"=@Reason,
                      "Revision"="Revision"+1,"UpdatedAt"=CURRENT_TIMESTAMP,"UpdatedBy"=@Actor
                    WHERE "TenantId"=@TenantId AND "Id"=@mappingId AND "Revision"=@Revision AND "ReversedAt" IS NULL
                    """,new{mappingId,Revision=input.ExpectedMappingRevision,Reason=input.Reason.Trim(),Actor=tenant.UserId},ct:token);
                if(changed!=1)throw new DomainException("Mapping changed; refresh it.",412);
                mapping.Revision++;
                await store.InsertAsync(new PatientLinkJournal{MergeId=mapping.Id,Operation="reverse",EntityId=source.Id,AppliedAt=DateTime.UtcNow,PublicationHold=true,
                    BeforeValue=JsonSerializer.Serialize(new{canonicalPatientId=survivor.Id}),
                    AfterValue=JsonSerializer.Serialize(new{canonicalPatientId=source.Id,reviewVersionId=review.Id,reviewRevision=review.Revision,requiresPublicationReview=true})},ct:token);
                await AuditAsync(store,mapping,"reverse",token);
                return(new CommandReceipt(mapping.Id,mapping.Revision,CommandOutcome.Updated),200);
            },ct);
    }    public async Task<PatientMappingView> GetMappingAsync(Guid mappingId,CancellationToken ct)
    {
        PatientMappingView? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,CureFlowPermissions.IdentityMergeView,ct);
            await store.ExecuteAsync("SELECT pg_advisory_xact_lock_shared(hashtextextended(@scope,0))",new{scope=Scope},ct:ct);
            var mapping=await store.GetByIdAsync<PatientMerge>(mappingId,ct:ct)??throw new NotFoundException("Identity mapping");
            var entries=await store.QueryAsync<PatientLinkJournal>("""
                SELECT * FROM "PatientLinkJournals" WHERE "TenantId"=@TenantId AND "MergeId"=@mappingId ORDER BY "AppliedAt","Id" LIMIT 50
                """,new{mappingId},ct:ct);
            await audit.RecordAsync(new(mapping.SourcePatientId,"PatientIdentityMapping",mapping.Id,"history","authorized",mapping.Revision.ToString(),"registration"),ct,store);
            await audit.RecordAsync(new(mapping.SurvivorPatientId,"PatientIdentityMapping",mapping.Id,"history","authorized",mapping.Revision.ToString(),"registration"),ct,store);
            result=new(mapping.Id,mapping.SourcePatientId,mapping.SurvivorPatientId,mapping.ReviewVersionId,mapping.Revision,mapping.Reason,mapping.MergedAt,
                mapping.ReversedAt,mapping.ReversalReason,entries.Select(j=>new PatientMappingJournalEntry(j.Id,j.Operation,j.AppliedAt,j.CreatedBy,j.BeforeValue,j.AfterValue,j.PublicationHold)).ToArray());
        },ct);
        return result!;
    }}
