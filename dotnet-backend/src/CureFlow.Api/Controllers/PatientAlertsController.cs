using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients/{patientId:guid}/alerts")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientAlertsController(IPatientAlertService alerts):ControllerBase
{
    [HttpGet,Authorize(Policy="Permission:PatientAlerts.View")]
    public async Task<IActionResult> List(Guid patientId,CancellationToken ct)=>Ok(await alerts.ListAsync(patientId,ct));
    [HttpGet("{alertId:guid}/history"),Authorize(Policy="Permission:PatientAlerts.View")]
    public async Task<IActionResult> History(Guid patientId,Guid alertId,[FromQuery] long? before_revision=null,CancellationToken ct=default)
        =>Ok(await alerts.HistoryAsync(patientId,alertId,before_revision,ct));
    [HttpPost,Authorize(Policy="Permission:PatientAlerts.Manage")]
    public Task<IActionResult> Create(Guid patientId,PatientAlertInput input,CancellationToken ct)=>Save(patientId,null,input,ct);
    [HttpPut("{alertId:guid}"),Authorize(Policy="Permission:PatientAlerts.Manage")]
    public Task<IActionResult> Update(Guid patientId,Guid alertId,PatientAlertInput input,CancellationToken ct)=>Save(patientId,alertId,input,ct);
    private async Task<IActionResult> Save(Guid patientId,Guid? alertId,PatientAlertInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await alerts.SaveAsync(patientId,alertId,input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}
