using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/leads")]
public class LeadsController(ILeadService service) : ControllerBase
{
    [HttpGet, Authorize(Policy = "Permission:Lead.View")]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int page_size = 20, CancellationToken ct = default) =>
        Ok(await service.ListAsync(q, status, page, page_size, ct));

    [HttpGet("{id:guid}"), Authorize(Policy = "Permission:Lead.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.GetAsync(id, ct));

    [HttpPost, Authorize(Policy = "Permission:Lead.Create")]
    public async Task<IActionResult> Create(SaveLeadRequest request, CancellationToken ct) => Ok(new { id = await service.CreateAsync(request, ct) });

    [HttpPatch("{id:guid}"), Authorize(Policy = "Permission:Lead.Edit")]
    public async Task<IActionResult> Update(Guid id, SaveLeadRequest request, CancellationToken ct)
    { await service.UpdateAsync(id, request, ct); return Ok(new { ok = true }); }

    [HttpDelete("{id:guid}"), Authorize(Policy = "Permission:Lead.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    { await service.DeleteAsync(id, ct); return Ok(new { ok = true }); }

    [HttpPost("import-excel"), Authorize(Policy = "Permission:Lead.Create")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "Choose a non-empty file smaller than 10 MB." });
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".xlsx" or ".xls" or ".csv"))
            return BadRequest(new { message = "Choose an Excel (.xlsx, .xls) or CSV file." });
        using var stream = file.OpenReadStream();
        try { return Ok(await service.ImportAsync(stream, extension, ct)); }
        catch (ExcelDataReader.Exceptions.HeaderException)
        { return BadRequest(new { message = "The file is not a readable Excel or CSV workbook." }); }
    }

    [HttpPost("{id:guid}/appointment"), Authorize(Policy = "Permission:Lead.Convert")]
    [Authorize(Policy = "Permission:Patient.Create"), Authorize(Policy = "Permission:Appointment.Create")]
    public async Task<IActionResult> Book(Guid id, BookLeadAppointmentRequest request, CancellationToken ct) =>
        Ok(await service.BookAppointmentAsync(id, request, ct));
}
