using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/leads/{leadId:guid}/conversion"),Authorize(Policy="Permission:Lead.Convert")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class LeadConversionsController(ILeadConversionService conversions):ControllerBase
{
    [HttpGet("preview")]
    public async Task<IActionResult> Preview(Guid leadId,CancellationToken ct)=>Ok(await conversions.PreviewAsync(leadId,ct));
    [HttpGet]
    public async Task<IActionResult> Get(Guid leadId,CancellationToken ct)=>Ok(await conversions.GetAsync(leadId,ct));
    [HttpPost]
    public async Task<IActionResult> Convert(Guid leadId,LeadConversionInput input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await conversions.ConvertAsync(leadId,input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}