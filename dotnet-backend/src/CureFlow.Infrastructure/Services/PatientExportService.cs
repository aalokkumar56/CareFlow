using System.Globalization;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class PatientExportService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IOutboxWriter outbox,IPrivateDocumentStorage objects,IClinicalAccessAuditService audit,IConfiguration config)
    :IPatientExportService,IPreparedTransactionalOutboxConsumer
{
    public string EventType=>"patient.directory-export";
    public string ConsumerName=>"patient-directory-export";
    public bool SupportsSchema(int version)=>version==1;
    private static readonly HashSet<string> Fields=new(StringComparer.Ordinal){"patient_id","name","phone","email","age","gender"};
    private static Guid[] Ids(PatientExportJob job)=>JsonSerializer.Deserialize<Guid[]>(job.PatientIdsJson)??throw new InvalidOperationException("Export scope is missing.");
    private static Dictionary<Guid,string> Tags(PatientExportJob job)=>JsonSerializer.Deserialize<Dictionary<Guid,string>>(job.PatientTagsJson)??throw new InvalidOperationException("Export evidence is missing.");
    private async Task RequireActorAsync(ICureFlowDbSession store,Guid actorId,CancellationToken ct)
    {
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@actorId AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{actorId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains("Patient.Export",StringComparer.Ordinal)||!codes.Contains(CureFlowPermissions.PatientView,StringComparer.Ordinal))throw new ForbiddenException();
    }
    private async Task RequireCallerAsync(ICureFlowDbSession store,CancellationToken ct)
    {
        if(!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)throw new ForbiddenException();
        await RequireActorAsync(store,tenant.UserId.Value,ct);
    }
    private async Task<PatientExportJob> OwnerJobAsync(ICureFlowDbSession store,Guid id,CancellationToken ct)
    {
        var job=await store.QueryFirstOrDefaultAsync<PatientExportJob>("""
            SELECT * FROM "PatientExportJobs" WHERE "TenantId"=@TenantId AND "Id"=@id AND "RequesterId"=@actor FOR SHARE
            """,new{id,actor=tenant.UserId},ct:ct)??throw new NotFoundException("Export job");
        if(job.ExpiresAt<=DateTime.UtcNow)throw new DomainException("Export expired; retained artifact is not available for download.",410);
        return job;
    }
    private static async Task<List<Patient>> CurrentRowsAsync(ICureFlowDbSession store,PatientExportJob job,CancellationToken ct)
    {
        var ids=Ids(job);var tags=Tags(job);
        if(ids.Length is <1 or >200||ids.Distinct().Count()!=ids.Length||tags.Count!=ids.Length)throw new InvalidOperationException("Export scope exceeds its bounds.");
        var rows=(await store.QueryAsync<Patient>("""
            SELECT * FROM "Patients" p WHERE p."TenantId"=@TenantId AND p."Id"=ANY(@ids) AND p."IsActive" AND NOT p."IsDeleted"
              AND NOT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" s WHERE s."TenantId"=p."TenantId" AND s."PatientId"=p."Id" AND s."Restricted")
            ORDER BY p."Id" FOR SHARE
            """,new{ids},ct:ct)).ToList();
        if(rows.Count!=ids.Length||rows.Any(p=>!tags.TryGetValue(p.Id,out var tag)||tag!=PatientConcurrency.Tag(p)))
            throw new ConflictException("Export patient evidence or confidentiality changed; request a new explicit export.");
        return rows;
    }
    public Task<IdempotentCommandResult> CreateAsync(PatientExportInput input,string key,CancellationToken ct)
    {
        if(input.PatientIds is null||input.PatientIds.Count is <1 or >200||input.PatientIds.Any(x=>x==Guid.Empty)||input.PatientIds.Distinct().Count()!=input.PatientIds.Count
            ||input.Fields is null||input.Fields.Count is <1 or >6||input.Fields.Any(x=>!Fields.Contains(x))||input.Fields.Distinct().Count()!=input.Fields.Count
            ||string.IsNullOrWhiteSpace(input.Purpose)||input.Purpose.Length>500||input.Purpose.Any(char.IsControl))
            throw new ValidationException("Select 1 to 200 explicit patients, supported fields and a bounded export purpose.");
        var policy=config["PatientExports:ApprovedPolicyVersion"];var hours=config.GetValue<int?>("PatientExports:ApprovedRetryHorizonHours");
        var lifetime=config.GetValue<int?>("PatientExports:ApprovedArtifactLifetimeMinutes");
        if(string.IsNullOrWhiteSpace(policy)||policy.Length>100||policy.Any(char.IsControl)||hours is not >0 or >8760||lifetime is not >0 or >1440)
            throw new DomainException("The directory export policy/retry/artifact lifetime is not configured.",503);
        var ids=input.PatientIds.OrderBy(x=>x.ToString("N"),StringComparer.Ordinal).ToArray();
        return commands.ExecuteAsync("patient.directory-export.request",key,new{ids,input.Fields,input.Purpose},TimeSpan.FromHours(hours.Value),
            (store,token)=>RequireCallerAsync(store,token),async(store,token)=>{
                var rows=(await store.QueryAsync<Patient>("""
                    SELECT * FROM "Patients" p WHERE p."TenantId"=@TenantId AND p."Id"=ANY(@ids) AND p."IsActive" AND NOT p."IsDeleted"
                      AND NOT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" s WHERE s."TenantId"=p."TenantId" AND s."PatientId"=p."Id" AND s."Restricted")
                    ORDER BY p."Id" FOR SHARE
                    """,new{ids},ct:token)).ToList();
                if(rows.Count!=ids.Length)throw new NotFoundException("Current unrestricted patient export scope");
                var job=new PatientExportJob{RequesterId=tenant.UserId!.Value,PatientIdsJson=JsonSerializer.Serialize(ids),
                    PatientTagsJson=JsonSerializer.Serialize(rows.ToDictionary(p=>p.Id,PatientConcurrency.Tag)),FieldsJson=JsonSerializer.Serialize(input.Fields),
                    Purpose=input.Purpose.Trim(),PolicyVersion=policy,ExpiresAt=DateTime.UtcNow.AddMinutes(lifetime.Value)};
                await store.InsertAsync(job,ct:token);
                await outbox.WriteAsync(store,Guid.NewGuid(),EventType,1,"PatientExportJob",job.Id,"1",new{job.Id},token);
                foreach(var patient in rows)await audit.RecordAsync(new(patient.Id,"PatientExportJob",job.Id,"request","committed","1","directory-export"),token,store);
                return(new CommandReceipt(job.Id,1,CommandOutcome.Accepted),202);
            },ct);
    }
    public async Task<PatientExportStatus> GetAsync(Guid id,CancellationToken ct)
    {
        PatientExportStatus? status=null;
        await db.TransactionAsync(async store=>{
            await RequireCallerAsync(store,ct);var job=await OwnerJobAsync(store,id,ct);
            var rows=await CurrentRowsAsync(store,job,ct);
            foreach(var patient in rows)await audit.RecordAsync(new(patient.Id,"PatientExportJob",id,"read","authorized",job.Revision.ToString(),"directory-export"),ct,store);
            status=new(id,job.State,job.Revision,job.ExpiresAt);
        },ct);return status!;
    }
    public async Task<PatientExportDownload> DownloadAsync(Guid id,CancellationToken ct)
    {
        PatientExportJob? job=null;
        await db.TransactionAsync(async store=>{
            await RequireCallerAsync(store,ct);job=await OwnerJobAsync(store,id,ct);
            if(job.State!="ready"||job.ObjectKey is null||job.Sha256 is null||job.Length is null)throw new ConflictException("Export artifact is not ready.");
            foreach(var patient in await CurrentRowsAsync(store,job,ct))
                await audit.RecordAsync(new(patient.Id,"PatientExportJob",id,"download","authorized",job.Revision.ToString(),"directory-export"),ct,store);
        },ct);
        var stream=await objects.OpenReadAsync(tenant.TenantId,job!.ObjectKey!,job.Sha256!,job.Length!.Value,ct);
        return new(stream,$"patient-directory-{id:N}.csv");
    }
    public Task HandleAsync(ICureFlowDbSession transaction,OutboxEnvelope envelope,CancellationToken ct)
        =>throw new InvalidOperationException("Export preparation must precede local artifact commit.");
    private record Prepared(PatientExportJob Job,PrivateDocumentObject? Object);
    public async Task<object> PrepareAsync(OutboxEnvelope envelope,CancellationToken ct)
    {
        if(envelope.TenantId!=tenant.TenantId||envelope.AggregateType!="PatientExportJob"||envelope.AggregateVersion!="1")throw new ForbiddenException();
        PatientExportJob? job=null;byte[]? bytes=null;
        await db.TransactionAsync(async store=>{
            job=await store.GetByIdAsync<PatientExportJob>(envelope.AggregateId,ct:ct)??throw new NotFoundException("Export job");
            await RequireActorAsync(store,job.RequesterId,ct);
            if(job.ExpiresAt<=DateTime.UtcNow)throw new ConflictException("Export expired before preparation.");
            if(job.State=="ready")return;
            if(job.State!="pending")throw new ConflictException("Invalid export state.");
            var rows=await CurrentRowsAsync(store,job,ct);
            var fields=JsonSerializer.Deserialize<string[]>(job.FieldsJson)!;
            if(fields.Length is <1 or >6||fields.Any(f=>!Fields.Contains(f)))throw new InvalidOperationException("Unsupported stored export fields.");
            var builder=new StringBuilder();builder.AppendLine(string.Join(",",fields.Select(Cell)));
            foreach(var patient in rows)
            {
                builder.AppendLine(string.Join(",",fields.Select(field=>Cell(Value(patient,field)))));
                await audit.RecordAsync(new(patient.Id,"PatientExportJob",job.Id,"prepare","authorized","1","directory-export"),ct,store);
            }
            bytes=Encoding.UTF8.GetBytes(builder.ToString());
            if(bytes.Length>2*1024*1024)throw new InvalidOperationException("Export artifact exceeds its fixed bound.");
        },ct);
        if(job!.State=="ready")return new Prepared(job,null);
        using var source=new MemoryStream(bytes!);
        var item=await objects.StageAsync(tenant.TenantId,source,"text/csv",bytes!.Length,2*1024*1024,ct);
        await objects.PublishAsync(tenant.TenantId,item,ct);return new Prepared(job,item);
    }
    public async Task HandlePreparedAsync(ICureFlowDbSession store,OutboxEnvelope envelope,object preparation,CancellationToken ct)
    {
        if(preparation is not Prepared prepared||prepared.Job.Id!=envelope.AggregateId)throw new InvalidOperationException("Export preparation identity changed.");
        var job=await store.QueryFirstOrDefaultAsync<PatientExportJob>("""
            SELECT * FROM "PatientExportJobs" WHERE "TenantId"=@TenantId AND "Id"=@id FOR UPDATE
            """,new{id=envelope.AggregateId},ct:ct)??throw new NotFoundException("Export job");
        await RequireActorAsync(store,job.RequesterId,ct);
        if(job.ExpiresAt<=DateTime.UtcNow)throw new ConflictException("Export expired before artifact commit.");
        await CurrentRowsAsync(store,job,ct);
        if(job.State=="ready")return;
        if(job.State!="pending"||job.Revision!=1||prepared.Object is null)throw new ConflictException("Export source state changed.");
        await store.ExecuteAsync("""
            UPDATE "PatientExportJobs" SET "State"='ready',"Revision"=2,"ObjectKey"=@key,"ObjectVersion"=@version,
              "Sha256"=@hash,"Length"=@length,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "TenantId"=@TenantId AND "Id"=@id
            """,new{id=job.Id,key=prepared.Object.Key,version=prepared.Object.Version,hash=prepared.Object.Sha256,length=prepared.Object.Length},ct:ct);
    }
    private static string Value(Patient patient,string field)=>field switch{
        "patient_id"=>patient.Id.ToString("D"),"name"=>patient.Name,"phone"=>patient.Phone,"email"=>patient.Email??"",
        "age"=>patient.Age?.ToString(CultureInfo.InvariantCulture)??"","gender"=>patient.Gender.ToString(),_=>throw new InvalidOperationException()};
    public static string Cell(string value)
    {
        var inspected=value.TrimStart();
        if(inspected.Length>0&&(inspected[0] is '=' or '+' or '-' or '@'||value.Any(c=>c is '\t' or '\r' or '\n')))value="'"+value;
        return "\""+value.Replace("\"","\"\"")+"\"";
    }
}