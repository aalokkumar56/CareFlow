using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;
[ApiController,Route("api/facilities/{facilityId:guid}/resources")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class FacilityResourcesController(IFacilityResourceService resources):ControllerBase
{
    [HttpGet,Authorize(Policy="Permission:Organization.View")]
    public async Task<IActionResult> List(Guid facilityId,[FromQuery]bool eligibleOnly=false,[FromQuery]int limit=100,CancellationToken ct=default)=>Ok(await resources.ListAsync(facilityId,eligibleOnly,limit,ct));
    [HttpPost,Authorize(Policy="Permission:Organization.Manage")]
    public Task<IActionResult> Create(Guid facilityId,FacilityResourceSave request,CancellationToken ct)=>Save(facilityId,null,request,ct);
    [HttpPut("{id:guid}"),Authorize(Policy="Permission:Organization.Manage")]
    public Task<IActionResult> Update(Guid facilityId,Guid id,FacilityResourceSave request,CancellationToken ct)=>Save(facilityId,id,request,ct);
    private async Task<IActionResult> Save(Guid facilityId,Guid? id,FacilityResourceSave input,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await resources.SaveAsync(facilityId,id,input,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}