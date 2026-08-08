using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _svc;
    private readonly IValidator<CreatePatientRequest> _createValidator;
    private readonly IValidator<UpdatePatientRequest> _updateValidator;

    public PatientsController(
        IPatientService svc,
        IValidator<CreatePatientRequest> createValidator,
        IValidator<UpdatePatientRequest> updateValidator)
    {
        _svc = svc;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? department,
        [FromQuery] string? tag,
        [FromQuery] string? inquiry_source,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = Pagination.DefaultPageSize,
        CancellationToken ct = default) =>
        Ok(await _svc.ListAsync(q, status, department, tag, inquiry_source, page, page_size, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetAsync(id, ct));

    [HttpGet("{id:guid}/holistic-view")]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> HolisticView(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetHolisticViewAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "Permission:Patient.Create")]
    public async Task<IActionResult> Create([FromBody] CreatePatientRequest req, CancellationToken ct)
    {
        await ValidateAsync(_createValidator, req, ct);
        var id = await _svc.CreateAsync(req, ct);
        return Ok(new { id });
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Patient.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePatientRequest req, CancellationToken ct)
    {
        await ValidateAsync(_updateValidator, req, ct);
        await _svc.UpdateAsync(id, req, ct);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Patient.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(new { ok = true });
    }

    [HttpPost("import-csv")]
    [Authorize(Policy = "Permission:Patient.Create")]
    public async Task<IActionResult> ImportCsv(IFormFile file, CancellationToken ct)
    {
        using var s = file.OpenReadStream();
        var (inserted, skipped, blankRows, skipLog) = await _svc.ImportCsvAsync(s, ct);
        return Ok(new { inserted, skipped, blankRows, skipLog });
    }   

    [HttpPost("import-excel")]
    [Authorize(Roles = "admin,tenant_owner,Admin,TenantOwner")]
    public async Task<IActionResult> ImportExcel(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls" && ext != ".csv")
            return BadRequest(new { message = "Only .xlsx, .xls, and .csv files are supported" });

        using var stream = file.OpenReadStream();
        var result = await _svc.ImportExcelAsync(stream, ext, ct);
        return Ok(new
        {
            inserted = result.Inserted,
            skipped = result.Skipped,
            blankRows = result.BlankRows,
            skipLog = result.SkipLog   // per-row skip reasons
        });
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid)
            throw new Application.Common.ValidationException(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
