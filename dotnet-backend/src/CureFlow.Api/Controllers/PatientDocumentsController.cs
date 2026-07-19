using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api")]
public class PatientDocumentsController : ControllerBase
{
    private readonly IPatientDocumentService _svc;
    public PatientDocumentsController(IPatientDocumentService svc) => _svc = svc;

    [HttpGet("patients/{patientId:guid}/documents")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> List(Guid patientId, CancellationToken ct) =>
        Ok(await _svc.ListByPatientAsync(patientId, ct));

    [HttpPost("patients/{patientId:guid}/documents")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Upload(
        Guid patientId,
        [FromForm] Guid? visitId,
        [FromForm] string? title,
        [FromForm] string? documentType,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { detail = "File is required" });

        await using var stream = file.OpenReadStream();
        var result = await _svc.UploadAsync(
            patientId,
            visitId,
            title,
            documentType ?? "paper_note",
            stream,
            file.FileName,
            file.ContentType,
            file.Length,
            ct);
        return Ok(result);
    }

    [HttpGet("patient-documents/{id:guid}/download")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _svc.DownloadAsync(id, ct);
        return File(stream, contentType, fileName);
    }

    [HttpDelete("patient-documents/{id:guid}")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return NoContent();
    }
}
