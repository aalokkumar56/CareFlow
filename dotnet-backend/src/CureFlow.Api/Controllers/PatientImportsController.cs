using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patient-imports"),Authorize(Policy="Permission:Patient.Import")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientImportsController(IPatientImportService imports,Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> jsonOptions):ControllerBase
{
    [HttpPost,RequestSizeLimit(9*1024*1024)]
    public async Task<IActionResult> Stage(IFormFile file,[FromForm]string format,[FromForm]string mapping,CancellationToken ct)
    {
        PatientImportMapping input;
        try{input=JsonSerializer.Deserialize<PatientImportMapping>(mapping,jsonOptions.Value.JsonSerializerOptions)??throw new JsonException();}
        catch(JsonException){throw new ValidationException("Supply explicit import mapping JSON.");}
        await using var stream=file.OpenReadStream();
        return Receipt(await imports.StageAsync(stream,file.Length,format,input,Key(),ct));
    }
    [HttpGet("{batchId:guid}")]
    public async Task<IActionResult> Preview(Guid batchId,int afterRow=0,int limit=50,CancellationToken ct=default)
        =>Ok(await imports.PreviewAsync(batchId,afterRow,limit,ct));
    [HttpPost("{batchId:guid}/rows/{rowNumber:int}/decision")]
    public async Task<IActionResult> Commit(Guid batchId,int rowNumber,PatientImportRowDecision decision,CancellationToken ct)
        =>Receipt(await imports.CommitRowAsync(batchId,rowNumber,decision,Key(),ct));
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