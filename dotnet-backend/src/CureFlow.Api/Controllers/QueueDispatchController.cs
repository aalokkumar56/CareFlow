using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/facilities/{facilityId:guid}/queue-dispatch")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class QueueDispatchController(IQueueDispatchService dispatch):ControllerBase
{
    [HttpGet("policy"),Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Policy(Guid facilityId,CancellationToken ct)=>Ok(await dispatch.PolicyAsync(facilityId,ct));
    [HttpGet,Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Waiting(Guid facilityId,[FromQuery]DateOnly date,[FromQuery]int take=50,CancellationToken ct=default)
        =>Ok(await dispatch.WaitingAsync(facilityId,date,take,ct));
    [HttpGet("{queueEntryId:guid}"),Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Get(Guid facilityId,Guid queueEntryId,CancellationToken ct)
    {
        var row=await dispatch.GetAsync(facilityId,queueEntryId,ct);
        Response.Headers.ETag=$"\"queue:{queueEntryId:D}:{row.Revision}\"";
        return Ok(row);
    }
    [HttpGet("{queueEntryId:guid}/priority-history"),Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> History(Guid facilityId,Guid queueEntryId,[FromQuery]long afterRevision=0,[FromQuery]int take=50,CancellationToken ct=default)
        =>Ok(await dispatch.HistoryAsync(facilityId,queueEntryId,afterRevision,take,ct));
    public record PriorityInput(int Rank,string Reason);
    [HttpPost("{queueEntryId:guid}/priority"),Authorize(Policy="Permission:Appointment.Edit")]
    public async Task<IActionResult> Override(Guid facilityId,Guid queueEntryId,PriorityInput input,CancellationToken ct)
        =>Receipt(await dispatch.OverrideAsync(facilityId,queueEntryId,Revision(queueEntryId),input.Rank,input.Reason,Key(),ct));
    [HttpPost("{queueEntryId:guid}/start"),Authorize(Policy="Permission:Appointment.Edit")]
    public async Task<IActionResult> Start(Guid facilityId,Guid queueEntryId,CancellationToken ct)
        =>Receipt(await dispatch.StartAsync(facilityId,queueEntryId,Revision(queueEntryId),Key(),ct));
    private string Key()
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        return key[0]!;
    }
    private long Revision(Guid id)
    {
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1||string.IsNullOrWhiteSpace(tags[0]))throw new DomainException("Supply the current queue If-Match.",428);
        var tag=tags[0]!;var prefix=$"\"queue:{id:D}:";
        if(!tag.StartsWith(prefix,StringComparison.Ordinal)||!tag.EndsWith('"')||!long.TryParse(tag[prefix.Length..^1],out var revision)||revision<1)
            throw new DomainException("Use the current queue ETag.",412);
        return revision;
    }
    private IActionResult Receipt(IdempotentCommandResult result)
    {
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}
