using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/appointment-reservations")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AppointmentReservationsController(IAppointmentReservationService reservations):ControllerBase
{
    [HttpPost,Authorize(Policy="Permission:Appointment.Create")]
    public async Task<IActionResult> Hold(AppointmentHoldInput input,CancellationToken ct)=>Receipt(await reservations.HoldAsync(input,Key(),ct));
    [HttpGet("{id:guid}"),Authorize(Policy="Permission:Appointment.View")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await reservations.GetAsync(id,ct));
    [HttpPost("{id:guid}/release"),Authorize(Policy="Permission:Appointment.Create")]
    public async Task<IActionResult> Release(Guid id,[FromBody]long expectedRevision,CancellationToken ct)=>Receipt(await reservations.ReleaseAsync(id,expectedRevision,Key(),ct));
    private string Key()
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        return key[0]!;
    }
    private IActionResult Receipt(IdempotentCommandResult result)
    {
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}