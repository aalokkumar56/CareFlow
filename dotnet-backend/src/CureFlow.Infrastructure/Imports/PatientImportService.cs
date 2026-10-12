using System.Security.Cryptography;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Imports;
public sealed class PatientImportService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IPatientIdentifierService identifiers,IClinicalAccessAuditService audit,
    IPrivateDocumentStorage objects,IDocumentScanner scanner,IConfiguration config):IPatientImportService
{
    private static string Hash(object value)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private (string Policy,TimeSpan Horizon,TimeSpan Retention) Policy()
    {
        var policy=config["PatientImports:ApprovedPolicyVersion"];
        var horizon=config.GetValue<int?>("PatientImports:ApprovedRetryHorizonHours");
        var retention=config.GetValue<int?>("PatientImports:ApprovedSourceRetentionHours");
        if(string.IsNullOrWhiteSpace(policy)||policy.Length>100||policy.Any(char.IsControl)||horizon is not >0 or >8760||retention is not >0 or >87600)
            throw new DomainException("The patient import review/retry/source-retention policy is not configured.",503);
        return(policy,TimeSpan.FromHours(horizon.Value),TimeSpan.FromHours(retention.Value));
    }
    private async Task RequireAsync(ICureFlowDbSession store,CancellationToken ct,bool review=false)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@actor AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{actor=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        foreach(var capability in new[]{CureFlowPermissions.PatientImport,CureFlowPermissions.PatientCreate,CureFlowPermissions.PatientView,CureFlowPermissions.IdentityReviewView})
            if(!codes.Contains(capability,StringComparer.Ordinal))throw new ForbiddenException();
        if(review&&!codes.Contains(CureFlowPermissions.IdentityReviewManage,StringComparer.Ordinal))throw new ForbiddenException();
        var blocked=await store.QueryFirstOrDefaultAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" s WHERE s."TenantId"=@TenantId AND s."Restricted"
              AND NOT public.cureflow_patient_read_allowed(s."TenantId",s."PatientId"))
            """,ct:ct);
        if(blocked)throw new ForbiddenException();
    }
    private async Task<PatientImportBatch> BatchAsync(ICureFlowDbSession store,Guid id,CancellationToken ct)
    {
        var batch=await store.QueryFirstOrDefaultAsync<PatientImportBatch>("""
            SELECT * FROM "PatientImportBatches" WHERE "TenantId"=@TenantId AND "Id"=@id AND "CreatedBy"=@actor
            """,new{id,actor=tenant.UserId},ct:ct)??throw new NotFoundException("Import batch");
        if(batch.ExpiresAt<=DateTime.UtcNow)throw new ConflictException("Import source review window expired; retained evidence is not deleted.");
        _=await store.QueryFirstOrDefaultAsync<CureFlow.Domain.Entities.Saas.Facility>("""
            SELECT * FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@id AND "Id"=public.cureflow_lock_active_facility(@TenantId,@id)
            """,new{id=batch.FacilityId},ct:ct)??throw new ConflictException("Import facility is not active.");
        return batch;
    }
    public async Task<IdempotentCommandResult> StageAsync(Stream content,long length,string format,PatientImportMapping mapping,string key,CancellationToken ct)
    {
        PatientImportParser.ValidateMapping(mapping);var policy=Policy();
        if(length is <=0 or >PatientImportParser.MaximumBytes||format is not ("csv" or "xls" or "xlsx"))throw new ValidationException("Supply a bounded CSV/XLS/XLSX file.");
        await db.TransactionAsync(store=>RequireAsync(store,ct),ct);
        using var buffer=new MemoryStream();var bytesBuffer=new byte[81920];int count;
        while((count=await content.ReadAsync(bytesBuffer,ct))>0)
        {
            if(buffer.Length+count>PatientImportParser.MaximumBytes)throw new DomainException("Import exceeds 8 MiB.",413);
            await buffer.WriteAsync(bytesBuffer.AsMemory(0,count),ct);
        }
        if(buffer.Length!=length)throw new ValidationException("Import length changed during upload.");
        buffer.Position=0;var scan=await scanner.ScanAsync(buffer,ct);
        if(scan.Verdict!="clean"||string.IsNullOrWhiteSpace(scan.Engine)||scan.Engine=="unconfigured"||scan.Engine.Length>100||string.IsNullOrWhiteSpace(scan.EngineVersion)||scan.EngineVersion=="none"||scan.EngineVersion.Length>200)throw new DomainException("Import requires clean evidence from the approved local scanner.",scan.Verdict=="rejected"?422:503);
        var bytes=buffer.ToArray();var parsed=PatientImportParser.Parse(bytes,format,mapping,ct);
        if(parsed.Count==0)throw new ValidationException("Import has no data rows.");
        var mappingJson=JsonSerializer.Serialize(new{mapping.FacilityId,mapping.HeaderRow,Columns=mapping.Columns.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value),mapping.DateFormat,mapping.CsvSeparator,mapping.Worksheet,format});
        var fingerprint=Hash(mappingJson);var sha=Convert.ToHexString(SHA256.HashData(bytes));
        buffer.Position=0;var source=await objects.StageAsync(tenant.TenantId,buffer,"application/octet-stream",length,PatientImportParser.MaximumBytes,ct);
        if(source.Sha256!=sha||source.Length!=length)throw new ConflictException("Retained original hash/length does not match parsed source.");
        await objects.PublishAsync(tenant.TenantId,source,ct);
        return await commands.ExecuteAsync("patient.import.stage",key,new{sha,fingerprint},policy.Horizon,
            (store,token)=>RequireAsync(store,token),async(store,token)=>{
                _=await store.QueryFirstOrDefaultAsync<CureFlow.Domain.Entities.Saas.Facility>("""
                    SELECT * FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@id AND "Id"=public.cureflow_lock_active_facility(@TenantId,@id)
                    """,new{id=mapping.FacilityId},ct:token)??throw new NotFoundException("Active facility");
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))",new{key=$"patient-import:{tenant.TenantId}:{mapping.FacilityId}:{sha}:{fingerprint}"},ct:token);
                var existing=await store.QueryFirstOrDefaultAsync<PatientImportBatch>("""
                    SELECT * FROM "PatientImportBatches" WHERE "TenantId"=@TenantId AND "FacilityId"=@facility AND "FileSha256"=@sha AND "MappingFingerprint"=@fingerprint
                    """,new{facility=mapping.FacilityId,sha,fingerprint},ct:token);
                if(existing is not null)
                {
                    if(existing.CreatedBy!=tenant.UserId)throw new ConflictException("This original import has already been staged.");
                    return(new CommandReceipt(existing.Id,null,CommandOutcome.Accepted),200);
                }
                var batch=new PatientImportBatch{FacilityId=mapping.FacilityId,FileSha256=sha,Format=format,FileLength=length,
                    SourceObjectKey=source.Key,SourceObjectVersion=source.Version,MappingJson=mappingJson,MappingFingerprint=fingerprint,
                    ScanEngine=scan.Engine,ScanEngineVersion=scan.EngineVersion,PolicyVersion=policy.Policy,RowCount=parsed.Count,ExpiresAt=DateTime.UtcNow+policy.Retention};
                await store.InsertAsync(batch,ct:token);
                foreach(var row in parsed)
                    await store.InsertAsync(new PatientImportRow{BatchId=batch.Id,RowNumber=row.RowNumber,RawValuesJson=JsonSerializer.Serialize(row.RawValues),
                        DraftJson=row.Draft is null?null:JsonSerializer.Serialize(row.Draft),ErrorsJson=JsonSerializer.Serialize(row.Errors),
                        RepeatedFileRow=row.RepeatedFileRow,SourceHash=Hash(new{row.RowNumber,row.RawValues,row.Draft,row.Errors})},ct:token);
                await audit.RecordAsync(new(null,"PatientImportBatch",batch.Id,"stage","committed",null,"registration"),token,store);
                return(new CommandReceipt(batch.Id,null,CommandOutcome.Accepted),201);
            },ct);
    }
    private async Task<PatientImportPreviewRow> PreviewRowAsync(ICureFlowDbSession store,PatientImportRow row,CancellationToken ct)
    {
        await RequireAsync(store,ct);
        var draft=row.DraftJson is null?null:JsonSerializer.Deserialize<PatientImportDraft>(row.DraftJson);
        var candidates=draft is null?new List<Patient>(): (await store.QueryAsync<Patient>("""
            SELECT * FROM "Patients" WHERE "TenantId"=@TenantId AND NOT "IsDeleted"
              AND (lower(btrim("Name"))=lower(@name) OR (@phone<>'' AND regexp_replace("Phone",'[^0-9]','','g')=@phone))
              ORDER BY "Id" LIMIT 6 FOR SHARE
            """,new{name=draft.Name,phone=new string(draft.Phone.Where(char.IsAsciiDigit).ToArray())},ct:ct)).ToList();
        if(candidates.Count>5)throw new ConflictException("Too many potential duplicate charts; use the identity review queue before importing this row.");
        foreach(var candidate in candidates)await audit.RecordAsync(new(candidate.Id,"PatientImportCandidate",candidate.Id,"read","authorized",null,"registration"),ct,store);
        var projections=candidates.Select(p=>new PatientImportCandidate(p.Id,p.Name,p.Phone,PatientConcurrency.Tag(p))).ToArray();
        var tag=Hash(new{row.SourceHash,row.RepeatedFileRow,Candidates=projections.Select(x=>new{x.PatientId,x.PatientTag}).ToArray()});
        return new(row.RowNumber,JsonSerializer.Deserialize<string[]>(row.RawValuesJson)!,draft,JsonSerializer.Deserialize<string[]>(row.ErrorsJson)!,
            row.RepeatedFileRow,projections,tag,row.Decision,row.PatientId);
    }
    public async Task<PatientImportPreview> PreviewAsync(Guid batchId,int afterRow,int limit,CancellationToken ct)
    {
        if(afterRow<0||limit is <1 or >100)throw new ValidationException("Use a positive row cursor and page size from 1 to 100.");
        PatientImportPreview? result=null;
        await db.TransactionAsync(async store=>{
            await RequireAsync(store,ct);var batch=await BatchAsync(store,batchId,ct);
            var rows=(await store.QueryAsync<PatientImportRow>("""
                SELECT * FROM "PatientImportRows" WHERE "TenantId"=@TenantId AND "BatchId"=@batchId AND "RowNumber">@afterRow ORDER BY "RowNumber" LIMIT @take
                """,new{batchId,afterRow,take=limit+1},ct:ct)).ToList();
            var views=new List<PatientImportPreviewRow>();
            foreach(var row in rows.Take(limit))views.Add(await PreviewRowAsync(store,row,ct));
            result=new(batchId,batch.FacilityId,batch.FileSha256,batch.Format,batch.RowCount,batch.ExpiresAt,views,rows.Count>limit?views[^1].RowNumber:null,batch.ParseErrorCode);
            await audit.RecordAsync(new(null,"PatientImportBatch",batchId,"preview","authorized",null,"registration"),ct,store);
        },ct);
        return result!;
    }
    public Task<IdempotentCommandResult> CommitRowAsync(Guid batchId,int rowNumber,PatientImportRowDecision decision,string key,CancellationToken ct)
    {
        var policy=Policy();
        if(rowNumber<=0||decision.Decision is not ("create_new" or "skip")||string.IsNullOrWhiteSpace(decision.Reason)||decision.Reason.Length>500
            ||decision.Reason.Any(char.IsControl)||string.IsNullOrEmpty(decision.ExpectedReviewTag)||decision.ExpectedReviewTag.Length!=64||decision.ExpectedReviewTag.Any(c=>!char.IsAsciiHexDigit(c)))
            throw new ValidationException("Supply a current preview tag, explicit create/skip decision and bounded reason.");
        return commands.ExecuteAsync("patient.import.row",key,new{batchId,rowNumber,decision},policy.Horizon,
            async(store,token)=>{
                await RequireAsync(store,token);await BatchAsync(store,batchId,token);
                var visible=await store.QueryFirstOrDefaultAsync<PatientImportRow>("""
                    SELECT * FROM "PatientImportRows" WHERE "TenantId"=@TenantId AND "BatchId"=@batchId AND "RowNumber"=@rowNumber
                    """,new{batchId,rowNumber},ct:token)??throw new NotFoundException("Import row");
                if(visible.PatientId.HasValue&&await store.GetByIdAsync<Patient>(visible.PatientId.Value,ct:token) is null)throw new NotFoundException("Patient");
            },
            async(store,token)=>{
                var batch=await BatchAsync(store,batchId,token);
                var row=await store.QueryFirstOrDefaultAsync<PatientImportRow>("""
                    SELECT * FROM "PatientImportRows" WHERE "TenantId"=@TenantId AND "BatchId"=@batchId AND "RowNumber"=@rowNumber FOR UPDATE
                    """,new{batchId,rowNumber},ct:token)??throw new NotFoundException("Import row");
                if(row.Decision!="pending")throw new ConflictException("This import row already has an immutable decision.");
                var view=await PreviewRowAsync(store,row,token);
                if(view.ReviewTag!=decision.ExpectedReviewTag)throw new DomainException("Candidate evidence changed; review this row again.",412);
                Guid? patientId=null;
                if(decision.Decision=="create_new")
                {
                    if(view.Draft is null||view.Errors.Count!=0)throw new ConflictException("Correct source errors and stage a new original before creating this row.");
                    if(view.Candidates.Count>0||view.RepeatedFileRow)
                    {
                        if(!decision.AcknowledgePotentialDuplicates)throw new ConflictException("Explicitly acknowledge possible duplicates or skip this row.");
                        await RequireAsync(store,token,true);
                    }
                    var draft=view.Draft;
                    var patient=new Patient{Name=draft.Name,Phone=draft.Phone,Email=draft.Email,Age=draft.Age,Gender=draft.Gender,
                        BirthDatePrecision="unknown",InquirySource="reviewed_import"};
                    PatientDemographicRules.Apply(patient,draft.BirthDate);
                    await store.ExecuteAsync("""
                        SELECT set_config('app.demographic_source_type','import',true),
                          set_config('app.demographic_source_reference',@reference,true),set_config('app.demographic_reason',@reason,true)
                        """,new{reference=$"import:{batchId:N}:row:{rowNumber}",reason=decision.Reason.Trim()},ct:token);
                    await store.InsertAsync(patient,ct:token);
                    await identifiers.AllocateLocalMrnAsync(store,batch.FacilityId,patient.Id,token);
                    var persisted=await store.GetByIdAsync<Patient>(patient.Id,ct:token)??throw new NotFoundException("Patient");
                    foreach(var candidate in view.Candidates)
                    {
                        var first=string.CompareOrdinal(patient.Id.ToString("N"),candidate.PatientId.ToString("N"))<0?patient.Id:candidate.PatientId;
                        await store.InsertAsync(new PatientIdentityReviewVersion{ReviewId=Guid.NewGuid(),FirstPatientId=first,
                            SecondPatientId=first==patient.Id?candidate.PatientId:patient.Id,Revision=1,Decision="open",
                            EvidenceReference=$"import:{batchId:N}:row:{rowNumber}",Reason=decision.Reason.Trim(),
                            FirstPatientTag=first==patient.Id?PatientConcurrency.Tag(persisted):candidate.PatientTag,
                            SecondPatientTag=first==patient.Id?candidate.PatientTag:PatientConcurrency.Tag(persisted),
                            ExactNameSignal=string.Equals(candidate.Name,draft.Name,StringComparison.OrdinalIgnoreCase),
                            SharedPhoneSignal=candidate.Phone==draft.Phone},ct:token);
                    }
                    patientId=patient.Id;
                    await audit.RecordAsync(new(patient.Id,"PatientImportRow",row.Id,"register","committed",null,"registration"),token,store);
                }
                await store.ExecuteAsync("""
                    UPDATE "PatientImportRows" SET "Decision"=@decision,"PatientId"=@patientId,"ReviewReason"=@reason,
                      "ReviewedBy"=@actor,"ReviewedAt"=CURRENT_TIMESTAMP,"ReviewPolicyVersion"=@policy,"UpdatedBy"=@actor,"UpdatedAt"=CURRENT_TIMESTAMP
                    WHERE "TenantId"=@TenantId AND "Id"=@id
                    """,new{decision=decision.Decision,patientId,reason=decision.Reason.Trim(),actor=tenant.UserId,policy=policy.Policy,id=row.Id},ct:token);
                await audit.RecordAsync(new(patientId,"PatientImportRow",row.Id,decision.Decision,"committed",null,"registration"),token,store);
                return(new CommandReceipt(patientId??row.Id,rowNumber,decision.Decision=="create_new"?CommandOutcome.Created:CommandOutcome.Completed),decision.Decision=="create_new"?201:200);
            },ct);
    }
}