using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients/{patientId:guid}/demographic-history")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientDemographicsController(IPatientDemographicHistoryService history):ControllerBase
{
    [HttpGet,Authorize(Policy="Permission:Patient.View")]
    public async Task<IActionResult> Get(Guid patientId,[FromQuery] long? before_revision=null,[FromQuery] int limit=50,CancellationToken ct=default)
        =>Ok(await history.GetAsync(patientId,before_revision,limit,ct));
}
