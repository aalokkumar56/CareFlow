using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/facilities/{facilityId:guid}/appointment-no-shows")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AppointmentNoShowsController(IAppointmentNoShowService noShows):ControllerBase
{
    [HttpGet("{appointmentId:guid}"),Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Get(Guid facilityId,Guid appointmentId,CancellationToken ct)
        =>Ok(await noShows.GetAsync(facilityId,appointmentId,ct));
    [HttpPost("{appointmentId:guid}"),Authorize(Policy="Permission:Appointment.Edit")]
    public async Task<IActionResult> Record(Guid facilityId,Guid appointmentId,AppointmentNoShowInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var tags=Request.Headers.IfMatch;
        if(tags.Count!=1||string.IsNullOrWhiteSpace(tags[0]))throw new DomainException("Supply the current appointment If-Match.",428);
        var tag=tags[0]!;var prefix=$"\"appointment:{appointmentId:D}:";
        if(!tag.StartsWith(prefix,StringComparison.Ordinal)||!tag.EndsWith('"')||!long.TryParse(tag[prefix.Length..^1],out var revision)||revision<1)
            throw new DomainException("Use the current appointment ETag.",412);
        var result=await noShows.RecordAsync(facilityId,appointmentId,revision,input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}
