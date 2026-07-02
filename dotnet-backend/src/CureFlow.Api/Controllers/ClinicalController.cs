using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/clinical")]
public class ClinicalController : ControllerBase
{
    private readonly IClinicalRecordService _svc;
    private readonly IValidator<CreateVitalSignsRequest> _vitalsValidator;

    public ClinicalController(
        IClinicalRecordService svc,
        IValidator<CreateVitalSignsRequest> vitalsValidator)
    {
        _svc = svc;
        _vitalsValidator = vitalsValidator;
    }

    [HttpPost("vitals")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> AddVitals([FromBody] CreateVitalSignsRequest request, CancellationToken ct)
    {
        var validation = await _vitalsValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()) });

        var id = await _svc.AddVitalsAsync(request, ct);
        return Ok(new { id });
    }

    [HttpGet("vitals/patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ListVitals(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListVitalsAsync(patientId, ct));

    [HttpPost("notes")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> AddNote([FromBody] CreateClinicalNoteRequest r, CancellationToken ct)
    {
        var id = await _svc.AddNoteAsync(r, ct);
        return Ok(new { id });
    }

    [HttpGet("notes/patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ListNotes(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListNotesAsync(patientId, ct));

    [HttpPatch("notes/{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateClinicalNoteRequest r, CancellationToken ct)
    {
        await _svc.UpdateNoteAsync(id, r, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("medical-history")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> AddMedical([FromBody] CreateMedicalHistoryRequest r, CancellationToken ct)
    {
        var id = await _svc.AddMedicalHistoryAsync(r, ct);
        return Ok(new { id });
    }

    [HttpGet("medical-history/patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ListMedicalHistory(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListMedicalHistoryAsync(patientId, ct));

    [HttpPost("family-history")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> AddFamily([FromBody] CreateFamilyHistoryRequest r, CancellationToken ct)
    {
        var id = await _svc.AddFamilyHistoryAsync(r, ct);
        return Ok(new { id });
    }

    [HttpGet("family-history/patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ListFamilyHistory(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListFamilyHistoryAsync(patientId, ct));
}
