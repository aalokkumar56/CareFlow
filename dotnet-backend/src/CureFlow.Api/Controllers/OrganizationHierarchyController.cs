using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/organization/{category}")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class OrganizationHierarchyController(IOrganizationHierarchyService hierarchy):ControllerBase
{
    [HttpGet]
    [Authorize(Policy="Permission:Organization.View")]
    public async Task<IActionResult> Children(Guid facilityId,string category,[FromQuery] Guid? parentId,[FromQuery] int limit=100,CancellationToken ct=default)=>
        Ok(await hierarchy.ChildrenAsync(facilityId,category,parentId,limit,ct));
    [HttpGet("{id:guid}/history")]
    [Authorize(Policy="Permission:Organization.View")]
    public async Task<IActionResult> History(Guid facilityId,string category,Guid id,[FromQuery]long beforeRevision=0,[FromQuery]int limit=50,CancellationToken ct=default)=>
        Ok(await hierarchy.HistoryAsync(facilityId,category,id,beforeRevision,limit,ct));
    [HttpPost]
    [Authorize(Policy="Permission:Organization.Manage")]
    public Task<IActionResult> Create(Guid facilityId,string category,OrganizationNodeSave request,CancellationToken ct)=>Save(facilityId,category,null,request,ct);
    [HttpPut("{id:guid}")]
    [Authorize(Policy="Permission:Organization.Manage")]
    public Task<IActionResult> Update(Guid facilityId,string category,Guid id,OrganizationNodeSave request,CancellationToken ct)=>Save(facilityId,category,id,request,ct);
    private async Task<IActionResult> Save(Guid facilityId,string category,Guid? id,OrganizationNodeSave request,CancellationToken ct)
    {
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await hierarchy.SaveAsync(facilityId,category,id,request,key[0]!,ct);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
}
