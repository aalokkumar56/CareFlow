using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/appointment-bookings/{id:guid}"),Authorize(Policy="Permission:Appointment.Edit"),
 ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AppointmentBookingChangesController(IAppointmentBookingChangeService changes):ControllerBase
{
    [HttpGet("changes")]
    public async Task<IActionResult> History(Guid id,[FromQuery]long afterRevision=0,[FromQuery]int limit=50,CancellationToken ct=default)
        =>Ok(await changes.HistoryAsync(id,afterRevision,limit,ct));
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel(Guid id,AppointmentCancelInput input,CancellationToken ct)
        =>Receipt(await changes.CancelAsync(id,Revision(id),input,Key(),ct));
    [HttpPost("reschedule")]
    public async Task<IActionResult> Reschedule(Guid id,AppointmentRescheduleInput input,CancellationToken ct)
        =>Receipt(await changes.RescheduleAsync(id,Revision(id),input,Key(),ct));
    private long Revision(Guid id)
    {
        var values=Request.Headers.IfMatch;
        if(values.Count==0)throw new DomainException("Supply the current appointment If-Match ETag.",428);
        var prefix=$"\"appointment:{id:D}:";
        var value=values.Count==1?values[0]:null;
        if(value is null||!value.StartsWith(prefix,StringComparison.Ordinal)||!value.EndsWith('"')
            ||!long.TryParse(value[prefix.Length..^1],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var revision)||revision<1)
            throw new ValidationException("Supply one strong current appointment ETag.");
        return revision;
    }
    private string Key()
    {
        var value=Request.Headers["Idempotency-Key"];
        if(value.Count!=1||string.IsNullOrWhiteSpace(value[0]))throw new ValidationException("Supply one Idempotency-Key.");
        return value[0]!;
    }
    private IActionResult Receipt(IdempotentCommandResult result)
    {
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        Response.Headers.ETag=$"\"appointment:{result.Receipt.ResourceId:D}:{result.Receipt.Revision}\"";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}