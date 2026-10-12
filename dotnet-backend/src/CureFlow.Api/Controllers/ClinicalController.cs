using CureFlow.Application.Common;
using System.Globalization;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/clinical")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
        return NoteResponse(await _svc.GetNoteAsync(id, ct));
    }

    [HttpGet("notes/patient/{patientId:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> ListNotes(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListNotesAsync(patientId, ct));

    [HttpPatch("notes/{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpdateClinicalNoteRequest r, CancellationToken ct)
    {
        r = WithExpectedRevision(id, r);
        await _svc.UpdateNoteAsync(id, r, ct);
        return NoteResponse(await _svc.GetNoteAsync(id, ct));
    }

    [HttpGet("notes/{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> GetNote(Guid id, CancellationToken ct) =>
        NoteResponse(await _svc.GetNoteAsync(id, ct));

    private IActionResult NoteResponse(ClinicalNoteDto note)
    {
        Response.Headers.ETag = $"\"note-{note.Id:N}-r{note.Revision}\"";
        return Ok(note);
    }

    private UpdateClinicalNoteRequest WithExpectedRevision(Guid id, UpdateClinicalNoteRequest request)
    {
        var header = Request.Headers.IfMatch.ToString();
        if (!string.IsNullOrEmpty(header))
        {
            // One strong tag tied to this note is required; wildcard tags cannot detect lost edits.
            var prefix = $"\"note-{id:N}-r";
            if (header.Length > 128 || !header.StartsWith(prefix, StringComparison.Ordinal)
                || !header.EndsWith('"')
                || !int.TryParse(header[prefix.Length..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
                || revision < 1)
                throw new DomainException("Supply a valid strong record ETag in If-Match.", 400);
            if (request.Revision.HasValue && request.Revision.Value != revision)
                throw new DomainException("The header and request record versions must match.", 400);
            request = request with { Revision = revision };
        }
        // Existing editors already submit Revision; preserve that equivalent precondition.
        if (!request.Revision.HasValue)
            throw new DomainException("Supply the current record version before saving.", 428);
        if (request.Revision.Value < 1)
            throw new DomainException("The record version must be positive.", 400);
        return request;
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
