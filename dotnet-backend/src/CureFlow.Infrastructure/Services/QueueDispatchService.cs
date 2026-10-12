using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;
namespace CureFlow.Infrastructure.Services;
public sealed class QueueDispatchService(ICureFlowDbSession db,ITenantContext tenant,IUserPermissionService permissions,
    IIdempotentCommandExecutor commands,IClinicalAccessAuditService audit,IConfiguration config):IQueueDispatchService
{
    private (QueueOrderingPolicy Policy,TimeSpan Retry) Configuration()
    {
        var version=config["QueueOrdering:ApprovedPolicyVersion"];
        var rank=config.GetValue<int?>("QueueOrdering:ApprovedDefaultRank");
        var hours=config.GetValue<int?>("QueueOrdering:ApprovedRetryHorizonHours");
        var bands=config.GetSection("QueueOrdering:ApprovedPriorityBands").Get<List<QueuePriorityBand>>();
        if(string.IsNullOrWhiteSpace(version)||version.Length>100||version.Any(char.IsControl)||rank is null
            ||hours is null or <=0 or >8760||bands is null||bands.Count is <1 or >20
            ||bands.Any(b=>b.Rank is <0 or >1000||string.IsNullOrWhiteSpace(b.Name)||b.Name.Length>80||b.Name.Any(char.IsControl))
            ||bands.Select(b=>b.Rank).Distinct().Count()!=bands.Count||!bands.Any(b=>b.Rank==rank.Value))
            throw new DomainException("Configure the approved queue ordering bands, default and retry policy.",503);
        return(new(version,rank.Value,bands.OrderBy(b=>b.Rank).ToArray()),TimeSpan.FromHours(hours.Value));
    }
    private async Task RequireAsync(ICureFlowDbSession store,Guid facilityId,bool writing,bool priority,bool complete,CancellationToken ct)
    {
        if(facilityId==Guid.Empty||!tenant.IsAuthenticated||tenant.IsPlatformUser||tenant.ServiceIdentity is not null||tenant.UserId is null)
            throw new ForbiddenException();
        var actor=await store.QueryFirstOrDefaultAsync<User>("""
            SELECT * FROM "Users" WHERE "TenantId"=@TenantId AND "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE
            """,new{id=tenant.UserId},ct:ct)??throw new ForbiddenException();
        var codes=await permissions.GetPermissionCodesAsync(actor,ct,store);
        if(!codes.Contains(writing?"Appointment.Edit":"Appointment.View",StringComparer.Ordinal)
            ||!codes.Contains("Patient.View",StringComparer.Ordinal)||!codes.Contains("Staff.View",StringComparer.Ordinal)
            ||priority&&!codes.Contains("Staff.Edit",StringComparer.Ordinal))throw new ForbiddenException();
        if(!await store.QuerySingleAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "Facilities" WHERE "TenantId"=@TenantId AND "Id"=@facilityId AND "IsActive" AND NOT "IsDeleted")
            """,new{facilityId},ct:ct))throw new NotFoundException("Active facility");
        // A hidden chart cannot silently disappear from the authoritative next-item decision.
        if(complete&&await store.QuerySingleAsync<bool>("""
            SELECT EXISTS(SELECT 1 FROM "PatientConfidentialityStates" WHERE "TenantId"=@TenantId AND "Restricted"
                AND NOT public.cureflow_patient_read_allowed("TenantId","PatientId"))
            """,ct:ct))throw new ForbiddenException();
    }
    private static async Task<QueueEntry> EntryAsync(ICureFlowDbSession store,Guid facilityId,Guid id,CancellationToken ct)
        =>await store.QueryFirstOrDefaultAsync<QueueEntry>("""
            SELECT * FROM "QueueEntries" WHERE "TenantId"=@TenantId AND "FacilityId"=@facilityId AND "Id"=@id
            """,new{facilityId,id},ct:ct)??throw new NotFoundException("Queue entry");
    private const string Projection="""
        SELECT q."Id" AS "QueueEntryId",q."AppointmentId",q."PatientId",q."Token",
            COALESCE(s."SortRank",p."SortRank",@defaultRank) AS "SortRank",COALESCE(s."Revision",p."Revision",1) AS "Revision",
            s."CreatedBy" AS "StartedBy",s."CreatedAt" AS "StartedAtUtc"
        FROM "QueueEntries" q
        LEFT JOIN LATERAL (SELECT * FROM "QueuePriorityOverrides" p WHERE p."TenantId"=q."TenantId" AND p."QueueEntryId"=q."Id"
            ORDER BY p."Revision" DESC LIMIT 1) p ON true
        LEFT JOIN "QueueStarts" s ON s."TenantId"=q."TenantId" AND s."QueueEntryId"=q."Id"
        """;
    private static async Task<QueueDispatchView> ViewAsync(ICureFlowDbSession store,Guid facilityId,Guid id,int defaultRank,CancellationToken ct)
        =>await store.QueryFirstOrDefaultAsync<QueueDispatchView>(Projection+"""
            WHERE q."TenantId"=@TenantId AND q."FacilityId"=@facilityId AND q."Id"=@id
            """,new{facilityId,id,defaultRank},ct:ct)??throw new NotFoundException("Queue entry");
    public async Task<QueueOrderingPolicy> PolicyAsync(Guid facilityId,CancellationToken ct)
    {
        await RequireAsync(db,facilityId,false,false,false,ct);
        var policy=Configuration().Policy;
        await audit.RecordAsync(new(null,"Facility",facilityId,"read_queue_policy","authorized",policy.Version,"operations"),ct);
        return policy;
    }
    public async Task<IReadOnlyList<QueueDispatchView>> WaitingAsync(Guid facilityId,DateOnly date,int take,CancellationToken ct)
    {
        if(date==default||take is <1 or >100)throw new ValidationException("Supply an arrival date and limit from 1 to 100.");
        await RequireAsync(db,facilityId,false,false,true,ct);
        var rows=await db.QueryAsync<QueueDispatchView>(Projection+"""
            WHERE q."TenantId"=@TenantId AND q."FacilityId"=@facilityId AND q."ArrivalDate"=@date AND s."Id" IS NULL
            ORDER BY COALESCE(p."SortRank",@defaultRank),q."Token" LIMIT @take
            """,new{facilityId,date,take,defaultRank=Configuration().Policy.DefaultRank},ct:ct);
        foreach(var row in rows)await audit.RecordAsync(new(row.PatientId,"QueueEntry",row.QueueEntryId,"read_waiting_order","authorized",row.Revision.ToString()),ct);
        return rows;
    }
    public async Task<QueueDispatchView> GetAsync(Guid facilityId,Guid queueEntryId,CancellationToken ct)
    {
        await RequireAsync(db,facilityId,false,false,false,ct);
        var row=await ViewAsync(db,facilityId,queueEntryId,Configuration().Policy.DefaultRank,ct);
        await audit.RecordAsync(new(row.PatientId,"QueueEntry",queueEntryId,"read_dispatch","authorized",row.Revision.ToString()),ct);
        return row;
    }
    public async Task<IReadOnlyList<QueuePriorityChangeView>> HistoryAsync(Guid facilityId,Guid queueEntryId,long afterRevision,int take,CancellationToken ct)
    {
        if(afterRevision<0||take is <1 or >100)throw new ValidationException("Supply a nonnegative revision cursor and limit from 1 to 100.");
        await RequireAsync(db,facilityId,false,false,false,ct);
        var entry=await EntryAsync(db,facilityId,queueEntryId,ct);
        var rows=await db.QueryAsync<QueuePriorityChangeView>("""
            SELECT "Id","Revision","SortRank","Reason","PolicyVersion","CreatedBy" AS "Actor","CreatedAt" AS "RecordedAtUtc"
            FROM "QueuePriorityOverrides" WHERE "TenantId"=@TenantId AND "QueueEntryId"=@queueEntryId AND "Revision">@afterRevision
            ORDER BY "Revision" LIMIT @take
            """,new{queueEntryId,afterRevision,take},ct:ct);
        await audit.RecordAsync(new(entry.PatientId,"QueueEntry",queueEntryId,"read_priority_history","authorized"),ct);
        return rows;
    }
    public Task<IdempotentCommandResult> OverrideAsync(Guid facilityId,Guid queueEntryId,long expectedRevision,int rank,string reason,string key,CancellationToken ct)
    {
        var configuration=Configuration();
        if(expectedRevision<1||!configuration.Policy.Bands.Any(b=>b.Rank==rank)||string.IsNullOrWhiteSpace(reason)
            ||reason.Trim().Length<3||reason.Length>500||reason.Any(char.IsControl))throw new ValidationException("Supply current revision, approved rank and a bounded attributable reason.");
        return commands.ExecuteAsync("queue.priority.override",key,new{facilityId,queueEntryId,expectedRevision,rank,reason=reason.Trim()},configuration.Retry,
            async(store,token)=>{await RequireAsync(store,facilityId,true,true,true,token);_=await EntryAsync(store,facilityId,queueEntryId,token);},
            async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                await RequireAsync(store,facilityId,true,true,true,token);
                var entry=await EntryAsync(store,facilityId,queueEntryId,token);
                var current=await ViewAsync(store,facilityId,queueEntryId,configuration.Policy.DefaultRank,token);
                if(current.StartedAtUtc.HasValue)throw new ConflictException("The queue item has already started.");
                if(current.Revision!=expectedRevision)throw new DomainException("The queue changed. Review its current version.",412);
                var row=new QueuePriorityOverride{QueueEntryId=entry.Id,PatientId=entry.PatientId,FacilityId=facilityId,ArrivalDate=entry.ArrivalDate,
                    Revision=checked(expectedRevision+1),SortRank=rank,Reason=reason.Trim(),PolicyVersion=configuration.Policy.Version};
                await store.InsertAsync(row,ct:token);
                await audit.RecordAsync(new(entry.PatientId,"QueuePriorityOverride",row.Id,"override_priority","committed",row.Revision.ToString()),token,store);
                return(new CommandReceipt(row.Id,row.Revision,CommandOutcome.Created),201);
            },ct);
    }
    public Task<IdempotentCommandResult> StartAsync(Guid facilityId,Guid queueEntryId,long expectedRevision,string key,CancellationToken ct)
    {
        var configuration=Configuration();
        if(expectedRevision<1)throw new ValidationException("Supply the current queue revision.");
        return commands.ExecuteAsync("queue.start",key,new{facilityId,queueEntryId,expectedRevision},configuration.Retry,
            async(store,token)=>{await RequireAsync(store,facilityId,true,false,true,token);_=await EntryAsync(store,facilityId,queueEntryId,token);},
            async(store,token)=>{
                await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)",ct:token);
                await RequireAsync(store,facilityId,true,false,true,token);
                var entry=await EntryAsync(store,facilityId,queueEntryId,token);
                var existing=await store.QueryFirstOrDefaultAsync<QueueStart>("""
                    SELECT * FROM "QueueStarts" WHERE "TenantId"=@TenantId AND "QueueEntryId"=@queueEntryId
                    """,new{queueEntryId},ct:token);
                if(existing is not null){
                    if(existing.CreatedBy!=tenant.UserId)throw new ConflictException("Another staff member has already started this queue item.");
                    await audit.RecordAsync(new(entry.PatientId,"QueueStart",existing.Id,"start","existing",existing.Revision.ToString()),token,store);
                    return(new CommandReceipt(existing.Id,existing.Revision,CommandOutcome.Completed),200);
                }
                var current=await ViewAsync(store,facilityId,queueEntryId,configuration.Policy.DefaultRank,token);
                if(current.Revision!=expectedRevision)throw new DomainException("The queue changed. Review its current version.",412);
                var first=await store.QueryFirstOrDefaultAsync<Guid?>("""
                    SELECT q."Id" FROM "QueueEntries" q LEFT JOIN LATERAL (SELECT p."SortRank" FROM "QueuePriorityOverrides" p
                      WHERE p."TenantId"=q."TenantId" AND p."QueueEntryId"=q."Id" ORDER BY p."Revision" DESC LIMIT 1) p ON true
                    WHERE q."TenantId"=@TenantId AND q."FacilityId"=@facilityId AND q."ArrivalDate"=@date
                     AND NOT EXISTS(SELECT 1 FROM "QueueStarts" s WHERE s."TenantId"=q."TenantId" AND s."QueueEntryId"=q."Id")
                    ORDER BY COALESCE(p."SortRank",@defaultRank),q."Token" LIMIT 1
                    """,new{facilityId,date=entry.ArrivalDate,defaultRank=configuration.Policy.DefaultRank},ct:token);
                if(first!=queueEntryId)throw new ConflictException("Start the current first queue item or record an authorized priority override.");
                var row=new QueueStart{QueueEntryId=entry.Id,PatientId=entry.PatientId,FacilityId=facilityId,ArrivalDate=entry.ArrivalDate,
                    Revision=checked(expectedRevision+1),SortRank=current.SortRank,DefaultRank=configuration.Policy.DefaultRank,PolicyVersion=configuration.Policy.Version};
                await store.InsertAsync(row,ct:token);
                await audit.RecordAsync(new(entry.PatientId,"QueueStart",row.Id,"start","committed",row.Revision.ToString()),token,store);
                return(new CommandReceipt(row.Id,row.Revision,CommandOutcome.Created),201);
            },ct);
    }
}
