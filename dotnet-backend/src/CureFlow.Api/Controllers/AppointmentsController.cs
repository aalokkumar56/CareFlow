using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _svc;
    private readonly IStaffService _staff;
    private readonly IValidator<CreateAppointmentRequest> _createValidator;
    private readonly IValidator<UpdateAppointmentRequest> _updateValidator;

    public AppointmentsController(
        IAppointmentService svc,
        IStaffService staff,
        IValidator<CreateAppointmentRequest> createValidator,
        IValidator<UpdateAppointmentRequest> updateValidator)
    {
        _svc = svc;
        _staff = staff;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet("booking-options")]
    [Authorize(Policy = "Permission:Appointment.View")]
    public async Task<IActionResult> BookingOptions(CancellationToken ct) =>
        Ok(await _staff.GetBookingOptionsAsync(ct));

    [HttpPost]
    [Authorize(Policy = "Permission:Appointment.Create")]
    public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest req, CancellationToken ct)
    {
        var result = await _createValidator.ValidateAsync(req, ct);
        if (!result.IsValid)
            throw new Application.Common.ValidationException(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));

        var id = await _svc.CreateAsync(
            req.PatientId, req.DoctorUserId, req.DoctorName, req.Department, req.ScheduledAt, req.Notes, ct);
        return Ok(new { id });
    }

    [HttpGet]
    [Authorize(Policy = "Permission:Appointment.View")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery(Name = "doctor_user_id")] Guid? doctorUserId,
        [FromQuery] int page = 1,
        [FromQuery] int page_size = Pagination.DefaultPageSize,
        CancellationToken ct = default) =>
        Ok(await _svc.ListAsync(status, from, to, doctorUserId, page, page_size, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Appointment.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetAsync(id, ct));

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Appointment.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAppointmentRequest req, CancellationToken ct)
    {
        var result = await _updateValidator.ValidateAsync(req, ct);
        if (!result.IsValid)
            throw new Application.Common.ValidationException(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));

        await _svc.UpdateAsync(id, req, ct);
        return Ok(new { ok = true });
    }

    public record UpdateStatusRequest(string Status);

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Permission:Appointment.Edit")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest r, CancellationToken ct)
    {
        await _svc.UpdateStatusAsync(id, r.Status, ct);
        return Ok(new { ok = true });
    }
}
