using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController, Route("api/facilities/{facilityId:guid}/queue-arrivals")]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class QueueArrivalsController(IQueueArrivalService arrivals) : ControllerBase
{
    [HttpPost("{appointmentId:guid}"), Authorize(Policy="Permission:Appointment.Edit")]
    public async Task<IActionResult> CheckIn(Guid facilityId, Guid appointmentId, CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1 || string.IsNullOrWhiteSpace(key[0])) throw new ValidationException("Supply one Idempotency-Key.");
        var tag=Request.Headers.IfMatch;
        if(tag.Count!=1 || string.IsNullOrWhiteSpace(tag[0])) throw new DomainException("Supply the current appointment If-Match.",428);
        var prefix=$"\"appointment:{appointmentId:D}:";
        var value=tag[0]!;
        if(!value.StartsWith(prefix,StringComparison.Ordinal)||!value.EndsWith('"')
            ||!long.TryParse(value[prefix.Length..^1],out var revision)||revision<1)
            throw new DomainException("Use the current appointment ETag.",412);
        var result=await arrivals.CheckInAsync(facilityId,appointmentId,revision,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
    [HttpGet("{appointmentId:guid}"), Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Get(Guid facilityId, Guid appointmentId, CancellationToken ct)
        =>Ok(await arrivals.GetAsync(facilityId,appointmentId,ct));
    [HttpGet, Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> List(Guid facilityId, [FromQuery]DateOnly date, [FromQuery]long? afterToken,
        [FromQuery]int take=50, CancellationToken ct=default)
        =>Ok(await arrivals.ListAsync(facilityId,date,afterToken,take,ct));
}
