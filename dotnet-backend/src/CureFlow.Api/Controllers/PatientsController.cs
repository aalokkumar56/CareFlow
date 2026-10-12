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
        [FromServices] IAuthorizationService authorization,
        [FromQuery] string? q,
        [FromQuery] string? status,
        [FromQuery] string? department,
        [FromQuery] string? tag,
        [FromQuery] string? inquiry_source,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = Pagination.DefaultPageSize,
        CancellationToken ct = default,
        [FromQuery] string sort_by = "name",
        [FromQuery] string sort_direction = "asc")
    {
        var canViewAppointments = (await authorization.AuthorizeAsync(User, null, "Permission:Appointment.View")).Succeeded;
        if (sort_by == "next_appointment" && !canViewAppointments) return Forbid();
        return Ok(await _svc.ListAsync(q, status, department, tag, inquiry_source, page, page_size, ct,
            sort_by, sort_direction, canViewAppointments));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result=await _svc.GetAsync(id,ct);
        Response.Headers.ETag=result.ConcurrencyTag;
        return Ok(result);
    }

    [HttpGet("search")]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? status,
        [FromQuery] string? department, [FromQuery] string? tag, [FromQuery] string? inquiry_source,
        [FromQuery] Guid? after_id = null, [FromQuery] int page_size = 50, CancellationToken ct = default)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _svc.SearchAsync(q, status, department, tag, inquiry_source, after_id, page_size, ct));
    }

    // BUG-03: stacked [Authorize] attributes replaced with explicit runtime check
    // to avoid ambiguity about AND-evaluation order across policy handlers.
    [HttpGet("{id:guid}/holistic-view")]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> HolisticView(
        Guid id,
        [FromServices] IAuthorizationService authorization,
        CancellationToken ct)
    {
        if (!(await authorization.AuthorizeAsync(User, null, "Permission:Clinical.View")).Succeeded)
            return Forbid();
        return Ok(await _svc.GetHolisticViewAsync(id, ct));
    }

    [HttpPost]
    [Authorize(Policy = "Permission:Patient.Create")]
    public async Task<IActionResult> Create([FromBody] CreatePatientRequest req, CancellationToken ct)
    {
        await ValidateAsync(_createValidator, req, ct);
        var key=Request.Headers["Idempotency-Key"];
        if(key.Count!=1||string.IsNullOrWhiteSpace(key[0]))throw new CureFlow.Application.Common.ValidationException("Supply one Idempotency-Key.");
        var result=await _svc.RegisterAsync(req,key[0]!,ct);
        Response.Headers.CacheControl="no-store";
        Response.Headers["Idempotency-Replayed"]=result.Replayed?"true":"false";
        return StatusCode(result.StatusCode,new{id=result.Receipt.ResourceId,receipt=result.Receipt});
    }

    [HttpGet("{id:guid}/identifiers")]
    [Authorize(Policy="Permission:Patient.View")]
    public async Task<IActionResult> Identifiers(Guid id,[FromQuery]Guid facility_id,[FromServices]IPatientIdentifierService identifiers,[FromServices]ICureFlowDbSession db,CancellationToken ct)
    {
        if(facility_id==Guid.Empty)throw new CureFlow.Application.Common.ValidationException("Supply an explicit identifier facility.");
        Response.Headers.CacheControl="no-store";
        return Ok(await identifiers.ListAsync(db,facility_id,id,ct));
    }
    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Patient.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePatientRequest req, CancellationToken ct)
    {
        await ValidateAsync(_updateValidator, req, ct);
        req.ExpectedETag=PatientConcurrency.RequireTag(id,Request.Headers.IfMatch.ToArray());
        await _svc.UpdateAsync(id, req, ct);
        Response.Headers.ETag=req.ResultETag;
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Patient.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(new { ok = true });
    }

    // BUG-01: added null/size/extension guard (mirrors ImportExcel)
    [HttpPost("import-csv")]
    [Authorize(Policy = "Permission:Patient.Create")]
    public async Task<IActionResult> ImportCsv(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file provided" });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".csv")
            return BadRequest(new { message = "Only .csv files are supported" });
        using var s = file.OpenReadStream();
        var (inserted, skipped, blankRows, skipLog) = await _svc.ImportCsvAsync(s, ct);
        return Ok(new { inserted, skipped, blankRows, skipLog });
    }

    // BUG-02: replaced role-string [Authorize] with permission policy
    [HttpPost("import-excel")]
    [Authorize(Policy = "Permission:Patient.Create")]
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
            skipLog = result.SkipLog,
        });
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (!result.IsValid)
            throw new Application.Common.ValidationException(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }
}
