using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/operating-settings")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class FacilityOperatingSettingsController(IFacilityOperatingSettingsService settings):ControllerBase
{
    [HttpGet]
    [Authorize(Policy="Permission:Settings.View")]
    public async Task<IActionResult> Get(Guid facilityId,CancellationToken ct)
    {
        var row=await settings.GetAsync(facilityId,ct);
        Response.Headers.ETag=Tag(facilityId,row?.Revision??0);
        return Ok(row);
    }
    [HttpGet("history")]
    [Authorize(Policy="Permission:Settings.View")]
    public async Task<IActionResult> History(Guid facilityId,[FromQuery]long before_revision=0,[FromQuery]int limit=50,CancellationToken ct=default)
        =>Ok(await settings.HistoryAsync(facilityId,before_revision,limit,ct));
    [HttpPut]
    [Authorize(Policy="Permission:Settings.Edit")]
    public async Task<IActionResult> Update(Guid facilityId,[FromBody]FacilityOperatingSettingsInput input,CancellationToken ct)
    {
        var matches=Request.Headers.IfMatch;
        if(matches.Count==0)throw new DomainException("Supply the facility settings ETag.",428);
        var prefix=$"\"facility-settings:{facilityId:N}:";
        var match=matches.Count==1?matches[0]:null;
        if(match is null||!match.StartsWith(prefix,StringComparison.Ordinal)||!match.EndsWith('"')
            ||!long.TryParse(match[prefix.Length..^1],System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out var revision)||revision<0)
            throw new ValidationException("Supply one resource-specific strong settings ETag.");
        var keys=Request.Headers["Idempotency-Key"];
        if(keys.Count!=1||string.IsNullOrWhiteSpace(keys[0]))throw new ValidationException("Supply one Idempotency-Key.");
        var result=await settings.UpdateAsync(facilityId,revision,keys[0]!,input,ct);
        Response.Headers.ETag=Tag(facilityId,result.Receipt.Revision!.Value);
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,result.Receipt);
    }
    private static string Tag(Guid id,long revision)=>$"\"facility-settings:{id:N}:{revision}\"";
}
