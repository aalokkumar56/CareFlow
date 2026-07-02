using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/allergies")]
public class AllergiesController : ControllerBase
{
    private readonly IAllergyService _svc;
    public AllergiesController(IAllergyService svc) => _svc = svc;

    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ByPatient(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListByPatientAsync(patientId, ct));

    [HttpPost]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Add([FromBody] CreateAllergyRequest req, CancellationToken ct)
    {
        var id = await _svc.AddAsync(req, ct);
        return Ok(new { id });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.RemoveAsync(id, ct);
        return Ok(new { ok = true });
    }
}
