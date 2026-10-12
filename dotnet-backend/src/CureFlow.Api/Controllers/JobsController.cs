using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class JobsController(IFailedJobService jobs) : ControllerBase
{
    [HttpGet("failed")]
    [Authorize(Policy = "Permission:Job.View")]
    public async Task<IActionResult> List([FromQuery] int limit = 50, CancellationToken ct = default) => Ok(await jobs.ListAsync(limit, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Job.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await jobs.GetAsync(id, ct);
        Response.Headers.ETag = Tag(id, result.Revision);
        return Ok(result);
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = "Permission:Job.Retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var match = Request.Headers.IfMatch;
        if (match.Count == 0) throw new DomainException("Supply the current job ETag.", 428);
        var prefix = $"\"job:{id:N}:";
        var value = match.Count == 1 ? match[0] : null;
        if (value is null || !value.StartsWith(prefix, StringComparison.Ordinal) || !value.EndsWith('"')
            || !long.TryParse(value[prefix.Length..^1], out var revision) || revision < 1)
            throw new DomainException("Supply one resource-specific strong job ETag.", 400);
        var key = Request.Headers["Idempotency-Key"];
        if (key.Count != 1 || string.IsNullOrWhiteSpace(key[0])) throw new DomainException("Supply one Idempotency-Key.", 400);
        var result = await jobs.RetryAsync(id, revision, key[0]!, ct);
        Response.Headers.ETag = Tag(id, result.Receipt.Revision!.Value);
        Response.Headers["Idempotency-Replayed"] = result.Replayed ? "true" : "false";
        return StatusCode(result.StatusCode, result.Receipt);
    }
    private static string Tag(Guid id, long revision) => $"\"job:{id:N}:{revision}\"";
}
