using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/lab-reports")]
public class LabReportsController : ControllerBase
{
    private readonly IVisitService _svc;
    public LabReportsController(IVisitService svc) => _svc = svc;

    [HttpPost("upload")]
    [Authorize(Policy = "Permission:Clinical.Edit")]
    public async Task<IActionResult> Upload(
        [FromForm] Guid patientId,
        [FromForm] string testName,
        [FromForm] Guid? visitId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { detail = "File is required" });

        await using var stream = file.OpenReadStream();
        var result = await _svc.UploadLabReportAsync(
            patientId, visitId, testName, stream, file.FileName, file.ContentType, file.Length, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/download")]
    [Authorize(Policy = "Permission:Clinical.View")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (stream, contentType, fileName) = await _svc.DownloadLabReportAsync(id, ct);
        return File(stream, contentType, fileName);
    }
}
