using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/named-follow-ups"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class NamedFollowUpsController(INamedFollowUpService service):ControllerBase
{
 [HttpPost,Authorize(Policy="Permission:FollowUp.Manage")]
 public async Task<IActionResult> Create(NamedFollowUpInput input,CancellationToken ct)=>Receipt(await service.CreateAsync(input,Key(),ct));
 [HttpGet("{id:guid}"),Authorize(Policy="Permission:FollowUp.View")]
 public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await service.GetAsync(id,ct));
 [HttpGet("{id:guid}/history"),Authorize(Policy="Permission:FollowUp.View")]
 public async Task<IActionResult> History(Guid id,[FromQuery]long? beforeRevision,CancellationToken ct)=>Ok(await service.HistoryAsync(id,beforeRevision,ct));
 [HttpGet("escalations"),Authorize(Policy="Permission:FollowUp.Escalate")]
 public async Task<IActionResult> Escalations([FromQuery]int limit=50,CancellationToken ct=default)=>Ok(await service.EscalationsAsync(limit,ct));
 public record Acknowledgement(long ExpectedRevision);
 [HttpPost("{id:guid}/acknowledge"),Authorize(Policy="Permission:FollowUp.Complete")]
 public async Task<IActionResult> Acknowledge(Guid id,Acknowledgement input,CancellationToken ct)=>Receipt(await service.AcknowledgeAsync(id,input.ExpectedRevision,Key(),ct));
 [HttpPost("{id:guid}/reassign"),Authorize(Policy="Permission:FollowUp.Manage")]
 public async Task<IActionResult> Reassign(Guid id,NamedFollowUpChange input,CancellationToken ct)=>Receipt(await service.ReassignAsync(id,input,Key(),ct));
 [HttpPost("{id:guid}/complete"),Authorize(Policy="Permission:FollowUp.Complete")]
 public async Task<IActionResult> Complete(Guid id,NamedFollowUpCompletion input,CancellationToken ct)=>Receipt(await service.CompleteAsync(id,input,Key(),ct));
 private string Key()
 {
  var value=Request.Headers["Idempotency-Key"];if(value.Count!=1||string.IsNullOrWhiteSpace(value[0]))throw new ValidationException("Supply one Idempotency-Key.");return value[0]!;
 }
 private IActionResult Receipt(IdempotentCommandResult result)
 { Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";return StatusCode(result.StatusCode,result.Receipt); }
}
