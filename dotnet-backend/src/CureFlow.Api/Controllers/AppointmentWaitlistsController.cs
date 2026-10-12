using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/appointment-waitlists"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class AppointmentWaitlistsController(IAppointmentWaitlistService waitlists):ControllerBase
{
 [HttpPost,Authorize(Policy="Permission:Appointment.Create")]
 public async Task<IActionResult> Add(AppointmentWaitlistInput input,CancellationToken ct)=>Receipt(await waitlists.AddAsync(input,Key(),ct));
 [HttpGet("{id:guid}"),Authorize(Policy="Permission:Appointment.View")]
 public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await waitlists.GetAsync(id,ct));
 [HttpPost("{id:guid}/offers"),Authorize(Policy="Permission:Appointment.Create")]
 public async Task<IActionResult> Offer(Guid id,AppointmentWaitlistOfferInput input,CancellationToken ct)=>Receipt(await waitlists.OfferAsync(id,input,Key(),ct));
 [HttpPost("{id:guid}/offers/{offerId:guid}/accept"),Authorize(Policy="Permission:Appointment.Create")]
 public async Task<IActionResult> Accept(Guid id,Guid offerId,AppointmentWaitlistAcceptanceInput input,CancellationToken ct)
  =>Receipt(await waitlists.AcceptAsync(id,offerId,input,Key(),ct));
 [HttpPost("{id:guid}/withdraw"),Authorize(Policy="Permission:Appointment.Edit")]
 public async Task<IActionResult> Withdraw(Guid id,AppointmentWaitlistWithdrawalInput input,CancellationToken ct)=>Receipt(await waitlists.WithdrawAsync(id,input,Key(),ct));
 private string Key()
 {
  var value=Request.Headers["Idempotency-Key"];if(value.Count!=1||string.IsNullOrWhiteSpace(value[0]))throw new ValidationException("Supply one Idempotency-Key.");
  return value[0]!;
 }
 private IActionResult Receipt(IdempotentCommandResult result)
 {
  Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";return StatusCode(result.StatusCode,result.Receipt);
 }
}