using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/patients/{patientId:guid}")]
public class PatientVisitsController : ControllerBase
{
    private readonly IVisitService _svc;
    public PatientVisitsController(IVisitService svc) => _svc = svc;

    [HttpGet("visits")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ListVisits(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListByPatientAsync(patientId, ct));

    [HttpGet("timeline")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> Timeline(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.GetTimelineAsync(patientId, ct));

    [HttpPost("visits")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> CreateVisit(Guid patientId, [FromBody] CreateVisitRequest req, CancellationToken ct)
    {
        if (req.PatientId == Guid.Empty)
            req = req with { PatientId = patientId };
        var id = await _svc.CreateAsync(req, ct);
        return Ok(new { id });
    }
}
