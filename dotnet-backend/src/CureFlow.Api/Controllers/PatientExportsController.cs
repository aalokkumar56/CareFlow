using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patient-exports"),Authorize(Policy="Permission:Patient.Export")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientExportsController(IPatientExportService exports):ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(PatientExportInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await exports.CreateAsync(input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id,CancellationToken ct)=>Ok(await exports.GetAsync(id,ct));
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id,CancellationToken ct)
    {
        var result=await exports.DownloadAsync(id,ct);
        return File(result.Content,"text/csv",result.FileName,enableRangeProcessing:false);
    }
}