using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:guid}/lifestyle")]
public class LifestyleController : ControllerBase
{
    private readonly ILifestyleService _svc;
    public LifestyleController(ILifestyleService svc) => _svc = svc;

    [HttpGet]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> Get(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.GetAsync(patientId, ct));

    [HttpPut]
    [Authorize(Policy = "Permission:Patient.Edit")]
    public async Task<IActionResult> Upsert(Guid patientId, [FromBody] LifestyleProfileDto dto, CancellationToken ct)
    {
        await _svc.UpsertAsync(patientId, dto, ct);
        return Ok(new { ok = true });
    }
}
