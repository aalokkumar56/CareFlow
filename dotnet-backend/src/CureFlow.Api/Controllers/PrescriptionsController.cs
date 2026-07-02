using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/prescriptions")]
public class PrescriptionsController : ControllerBase
{
    private readonly IPrescriptionService _svc;
    private readonly IVisitService _visitSvc;

    public PrescriptionsController(IPrescriptionService svc, IVisitService visitSvc)
    {
        _svc = svc;
        _visitSvc = visitSvc;
    }

    [HttpPost]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Create([FromBody] CreatePrescriptionRequest req, CancellationToken ct)
    {
        var id = await _svc.CreateAsync(req, ct);
        return Ok(new { id });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetAsync(id, ct));

    [HttpGet("patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ByPatient(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListByPatientAsync(patientId, ct));

    [HttpGet("{id:guid}/print")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> Print(Guid id, CancellationToken ct)
    {
        var html = await _visitSvc.GetPrescriptionPrintHtmlAsync(id, ct);
        return Content(html, "text/html");
    }
}
