using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/appointment-bookings"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AppointmentBookingsController(IAppointmentBookingService bookings):ControllerBase
{
    [HttpPost,Authorize(Policy="Permission:Appointment.Create")]
    public async Task<IActionResult> Book(AppointmentBookingInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await bookings.BookAsync(input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        if(result.Receipt.Revision.HasValue)Response.Headers.ETag=$"\"appointment:{result.Receipt.ResourceId:D}:{result.Receipt.Revision}\"";
        return StatusCode(result.StatusCode,result.Receipt);
    }
    [HttpGet("{id:guid}"),Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)
    {
        var result=await bookings.GetAsync(id,ct);Response.Headers.ETag=result.ETag;return Ok(result);
    }
}