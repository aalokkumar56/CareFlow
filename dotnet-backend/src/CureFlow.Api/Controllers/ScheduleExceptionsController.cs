using System.Globalization;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/facilities/{facilityId:guid}/schedule-exceptions")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ScheduleExceptionsController(IScheduleExceptionService exceptions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> List(Guid facilityId, [FromQuery] Guid? after_id, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await exceptions.ListAsync(facilityId, after_id, limit, ct));

    [HttpGet("{exceptionId:guid}")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> Get(Guid facilityId, Guid exceptionId, CancellationToken ct)
    {
        var row = await exceptions.GetAsync(facilityId, exceptionId, ct);
        Response.Headers.ETag = $"\"schedule-exception:{row.Id:N}:{row.Revision}\"";
        return Ok(row);
    }

    [HttpGet("reschedule-work")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> Work(Guid facilityId, [FromQuery] Guid? after_id, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await exceptions.WorkAsync(facilityId, after_id, limit, ct));

    [HttpPost]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> Create(Guid facilityId, [FromBody] ScheduleExceptionInput input, CancellationToken ct)
        => Receipt(await exceptions.CreateAsync(facilityId, Key(), input, ct));

    [HttpPost("{exceptionId:guid}/disable")]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> Disable(Guid facilityId, Guid exceptionId, CancellationToken ct)
    {
        var tags = Request.Headers.IfMatch;
        if (tags.Count == 0) throw new DomainException("Supply the current exception ETag.", 428);
        var prefix = $"\"schedule-exception:{exceptionId:N}:";
        var value = tags.Count == 1 ? tags[0] : null;
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith('"')
            || !long.TryParse(value[prefix.Length..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < 1)
            throw new ValidationException("Supply one strong resource-specific exception ETag.");
        return Receipt(await exceptions.DisableAsync(facilityId, exceptionId, revision, Key(), ct));
    }

    private string Key()
    {
        var values = Request.Headers["Idempotency-Key"];
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0])) throw new ValidationException("Supply one Idempotency-Key.");
        return values[0]!;
    }

    private IActionResult Receipt(IdempotentCommandResult result)
    {
        Response.Headers.ETag = $"\"schedule-exception:{result.Receipt.ResourceId:N}:{result.Receipt.Revision}\"";
        Response.Headers["Idempotency-Replayed"] = result.Replayed ? "true" : "false";
        return StatusCode(result.StatusCode, result.Receipt);
    }
}
