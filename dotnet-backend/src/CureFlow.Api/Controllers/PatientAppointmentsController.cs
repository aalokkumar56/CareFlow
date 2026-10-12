using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients/{patientId:guid}/appointments"),ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
[Authorize(Policy="Permission:Appointment.View")]
public sealed class PatientAppointmentsController(IPatientAppointmentQueryService service):ControllerBase
{
 [HttpGet]
 public async Task<IActionResult> List(Guid patientId,[FromQuery]DateTime fromUtc,[FromQuery]DateTime toUtc,
  [FromQuery]Guid? facilityId,[FromQuery]string? status,[FromQuery]int limit=50,
  [FromQuery]DateTime? afterScheduledAtUtc=null,[FromQuery]Guid? afterAppointmentId=null,CancellationToken ct=default)
  =>Ok(await service.ListAsync(patientId,new(fromUtc,toUtc,facilityId,status,limit,afterScheduledAtUtc,afterAppointmentId),ct));
 [HttpGet("next")]
 public async Task<IActionResult> Next(Guid patientId,[FromQuery]Guid? facilityId,CancellationToken ct)=>Ok(await service.NextAsync(patientId,facilityId,ct));
}
