using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/facilities/{facilityId:guid}/clinical-worklist")]
[Authorize(Policy="Permission:Clinical.View"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ClinicianWorklistController(IClinicianWorklistService service):ControllerBase
{
 [HttpGet]
 public async Task<IActionResult> List(Guid facilityId,[FromQuery]DateOnly date,[FromQuery]int limit=50,
  [FromQuery]DateTime? afterScheduledAtUtc=null,[FromQuery]Guid? afterAppointmentId=null,CancellationToken ct=default)
  =>Ok(await service.ListAsync(facilityId,new(date,limit,afterScheduledAtUtc,afterAppointmentId),ct));
}
