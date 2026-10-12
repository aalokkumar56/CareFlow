using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/patients")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class PatientMappingsController(IPatientMergeService mappings):ControllerBase
{
    [HttpGet("{patientId:guid}/identity-resolution"),Authorize(Policy="Permission:IdentityMerge.View")]
    public async Task<IActionResult> Resolve(Guid patientId,CancellationToken ct)=>Ok(await mappings.ResolveAsync(patientId,ct));
    [HttpPost("{survivorId:guid}/merge"),Authorize(Policy="Permission:IdentityMerge.Manage")]
    public async Task<IActionResult> Merge(Guid survivorId,PatientMergeInput request,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await mappings.MergeAsync(survivorId,request,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
    [HttpPost("identity-mappings/{mappingId:guid}/reverse"),Authorize(Policy="Permission:IdentityMerge.Reverse")]
    public async Task<IActionResult> Reverse(Guid mappingId,PatientMappingReversalInput request,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await mappings.ReverseAsync(mappingId,request,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }    [HttpGet("identity-mappings/{mappingId:guid}"),Authorize(Policy="Permission:IdentityMerge.View")]
    public async Task<IActionResult> History(Guid mappingId,CancellationToken ct)=>Ok(await mappings.GetMappingAsync(mappingId,ct));}
