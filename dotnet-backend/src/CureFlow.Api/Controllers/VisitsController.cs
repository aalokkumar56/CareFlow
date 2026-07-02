using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/visits")]
public class VisitsController : ControllerBase
{
    private readonly IVisitService _svc;
    public VisitsController(IVisitService svc) => _svc = svc;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetAsync(id, ct));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVisitRequest req, CancellationToken ct)
    {
        await _svc.UpdateAsync(id, req, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await _svc.CompleteAsync(id, ct);
        return Ok(new { ok = true });
    }
}
