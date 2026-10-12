using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients/{patientId:guid}/contacts")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientContactsController(IPatientContactService contacts):ControllerBase
{
    [HttpGet,Authorize(Policy="Permission:Patient.View")]
    public async Task<IActionResult> List(Guid patientId,[FromQuery]int limit=50,CancellationToken ct=default)=>Ok(await contacts.ListAsync(patientId,limit,ct));
    [HttpPost,Authorize(Policy="Permission:Patient.Edit")]
    public Task<IActionResult> Create(Guid patientId,PatientContactInput request,CancellationToken ct)=>Save(patientId,null,request,ct);
    [HttpPut("{contactId:guid}"),Authorize(Policy="Permission:Patient.Edit")]
    public Task<IActionResult> Update(Guid patientId,Guid contactId,PatientContactInput request,CancellationToken ct)=>Save(patientId,contactId,request,ct);
    private async Task<IActionResult> Save(Guid patientId,Guid? contactId,PatientContactInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await contacts.SaveAsync(patientId,contactId,input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}