using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _svc;

    public CampaignsController(ICampaignService svc) => _svc = svc;

    public record CreateCampaignRequest(string Name, string? Description, string MessageBody, object Audience, DateTime? ScheduledAt = null);

    public record ScheduleCampaignRequest(DateTime ScheduledAt);

    [HttpPost]
    [Authorize(Policy = "Permission:Campaign.Manage")]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest r, CancellationToken ct)
    {
        var id = await _svc.CreateAsync(r.Name, r.Description, r.MessageBody, r.Audience, r.ScheduledAt, ct);
        return Ok(new { id, status = r.ScheduledAt.HasValue ? "scheduled" : "draft" });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Campaign.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _svc.ListAsync(ct));

    [HttpGet("suggested-drafts")]
    [Authorize(Policy = "Permission:Campaign.View")]
    public async Task<IActionResult> SuggestedDrafts(CancellationToken ct) =>
        Ok(await _svc.GetSuggestedDraftsAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Campaign.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await _svc.GetAsync(id, ct));

    [HttpPost("preview-audience")]
    [Authorize(Policy = "Permission:Campaign.View")]
    public async Task<IActionResult> Preview([FromBody] object audience, CancellationToken ct)
    {
        var (count, sample) = await _svc.PreviewAudienceAsync(audience, ct);
        return Ok(new { count, sample });
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Campaign.Manage")]
    public async Task<IActionResult> Patch(Guid id, [FromBody] UpdateCampaignRequest update, CancellationToken ct)
    {
        await _svc.UpdateAsync(id, update, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:guid}/schedule")]
    [Authorize(Policy = "Permission:Campaign.Manage")]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleCampaignRequest request, CancellationToken ct)
    {
        await _svc.ScheduleAsync(id, request.ScheduledAt, ct);
        return Ok(new { ok = true, status = "scheduled" });
    }

    [HttpPost("{id:guid}/send")]
    [Authorize(Policy = "Permission:Campaign.Manage")]
    public async Task<IActionResult> Send(Guid id, CancellationToken ct)
    {
        var (sent, failed, total) = await _svc.SendAsync(id, ct);
        return Ok(new { sent, failed, total });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Campaign.Manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(new { ok = true });
    }
}
