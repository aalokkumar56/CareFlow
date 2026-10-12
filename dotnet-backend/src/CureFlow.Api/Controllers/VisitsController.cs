using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/visits"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class VisitsController(IVisitService service):ControllerBase
{
 [HttpGet("{id:guid}"),Authorize(Policy="Permission:Clinical.View")]
 public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await service.GetAsync(id,ct));
 [HttpGet("{id:guid}/state"),Authorize(Policy="Permission:Clinical.View")]
 public async Task<IActionResult> State(Guid id,CancellationToken ct)
 {
  var state=await service.GetEncounterStateAsync(id,ct);Response.Headers.ETag=$"\"encounter:{id:D}:{state.Revision}\"";return Ok(state);
 }
 [HttpGet("{id:guid}/transitions"),Authorize(Policy="Permission:Clinical.View")]
 public async Task<IActionResult> History(Guid id,[FromQuery]long? beforeRevision,CancellationToken ct)
  =>Ok(await service.EncounterHistoryAsync(id,beforeRevision,ct));
 [HttpPatch("{id:guid}"),Authorize(Policy="Permission:Clinical.Edit")]
 public async Task<IActionResult> Update(Guid id,UpdateVisitRequest input,CancellationToken ct)
 {
  var revision=EncounterCommandHeaders.Revision(this,"encounter",id,input.ExpectedRevision);
  return EncounterCommandHeaders.Result(this,await service.UpdateEncounterAsync(id,input with{ExpectedRevision=revision},EncounterCommandHeaders.Key(this),ct));
 }
 [HttpPost("{id:guid}/resume"),Authorize(Policy="Permission:Clinical.Edit")]
 public async Task<IActionResult> Resume(Guid id,EncounterResumeInput input,CancellationToken ct)
 {
  var revision=EncounterCommandHeaders.Revision(this,"encounter",id,input.ExpectedRevision);
  return EncounterCommandHeaders.Result(this,await service.ResumeEncounterAsync(id,input with{ExpectedRevision=revision},EncounterCommandHeaders.Key(this),ct));
 }
 [HttpPost("{id:guid}/complete"),Authorize(Policy="Permission:Clinical.Edit")]
 public async Task<IActionResult> Complete(Guid id,EncounterCompletionInput input,CancellationToken ct)
 {
  var revision=EncounterCommandHeaders.Revision(this,"encounter",id,input.ExpectedRevision);
  return EncounterCommandHeaders.Result(this,await service.CompleteEncounterAsync(id,input with{ExpectedRevision=revision},EncounterCommandHeaders.Key(this),ct));
 }
}
