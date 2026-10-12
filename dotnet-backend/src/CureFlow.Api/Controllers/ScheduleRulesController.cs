using System.Globalization;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/schedule-rules")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ScheduleRulesController(IScheduleRuleService schedules) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> List(Guid facilityId, [FromQuery] Guid? after_id, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await schedules.ListAsync(facilityId, after_id, limit, ct));

    [HttpGet("{ruleId:guid}")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> Get(Guid facilityId, Guid ruleId, CancellationToken ct)
    {
        var row = await schedules.GetAsync(facilityId, ruleId, ct);
        Response.Headers.ETag = $"\"schedule-rule:{row.Id:N}:{row.Revision}\"";
        return Ok(row);
    }

    [HttpGet("availability")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> Availability(Guid facilityId, [FromQuery] DateOnly from, [FromQuery] DateOnly through,
        [FromQuery] Guid? practitionerUserId, [FromQuery] Guid? resourceId, CancellationToken ct)
        => Ok(await schedules.AvailabilityAsync(facilityId, from, through, practitionerUserId, resourceId, ct));

    [HttpPost]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> Create(Guid facilityId, [FromBody] ScheduleRuleInput input, CancellationToken ct)
    {
        var result = await schedules.CreateAsync(facilityId, Key(), input, ct);
        return Receipt(result);
    }

    [HttpPost("{ruleId:guid}/disable")]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> Disable(Guid facilityId, Guid ruleId, CancellationToken ct)
    {
        var tags = Request.Headers.IfMatch;
        if (tags.Count == 0) throw new DomainException("Supply the current schedule rule ETag.", 428);
        var prefix = $"\"schedule-rule:{ruleId:N}:";
        var value = tags.Count == 1 ? tags[0] : null;
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith('"')
            || !long.TryParse(value[prefix.Length..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < 1)
            throw new ValidationException("Supply one strong resource-specific schedule ETag.");
        return Receipt(await schedules.DisableAsync(facilityId, ruleId, revision, Key(), ct));
    }

    private string Key()
    {
        var values = Request.Headers["Idempotency-Key"];
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) throw new ValidationException("Supply one Idempotency-Key.");
        return values[0]!;
    }

    private IActionResult Receipt(IdempotentCommandResult result)
    {
        Response.Headers.ETag = $"\"schedule-rule:{result.Receipt.ResourceId:N}:{result.Receipt.Revision}\"";
        Response.Headers["Idempotency-Replayed"] = result.Replayed ? "true" : "false";
        return StatusCode(result.StatusCode, result.Receipt);
    }
}
