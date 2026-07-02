using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/staff")]
public class StaffController : ControllerBase
{
    private readonly IStaffService _svc;
    public StaffController(IStaffService svc) => _svc = svc;

    [HttpGet("booking-options")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> BookingOptions(CancellationToken ct) =>
        Ok(await _svc.GetBookingOptionsAsync(ct));

    [HttpGet]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] UserRole? role, CancellationToken ct) =>
        Ok(await _svc.ListAsync(q, role, ct));

    [HttpGet("doctors")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> ListDoctors([FromQuery] string? q, CancellationToken ct) =>
        Ok(await _svc.ListDoctorsAsync(q, ct));

    [HttpGet("nurses")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> ListNurses([FromQuery] string? q, CancellationToken ct) =>
        Ok(await _svc.ListNursesAsync(q, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _svc.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = "Permission:Staff.Create")]
    public async Task<IActionResult> Create([FromBody] CreateStaffProfileRequest req, CancellationToken ct)
    {
        var id = await _svc.CreateAsync(req, ct);
        return Ok(new { id });
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStaffProfileRequest req, CancellationToken ct)
    {
        await _svc.UpdateAsync(id, req, ct);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Staff.Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _svc.DeleteAsync(id, ct);
        return Ok(new { ok = true });
    }

    [HttpGet("{staffProfileId:guid}/schedules")]
    [Authorize(Policy = "Permission:Staff.View")]
    public async Task<IActionResult> ListSchedules(Guid staffProfileId, CancellationToken ct) =>
        Ok(await _svc.ListSchedulesAsync(staffProfileId, ct));

    [HttpPost("schedules")]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> AddSchedule([FromBody] CreateDoctorScheduleRequest req, CancellationToken ct)
    {
        var id = await _svc.AddScheduleAsync(req, ct);
        return Ok(new { id });
    }

    [HttpPatch("schedules/{scheduleId:guid}")]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> UpdateSchedule(Guid scheduleId, [FromBody] UpdateDoctorScheduleRequest req, CancellationToken ct)
    {
        await _svc.UpdateScheduleAsync(scheduleId, req, ct);
        return Ok(new { ok = true });
    }

    [HttpDelete("schedules/{scheduleId:guid}")]
    [Authorize(Policy = "Permission:Staff.Edit")]
    public async Task<IActionResult> DeleteSchedule(Guid scheduleId, CancellationToken ct)
    {
        await _svc.DeleteScheduleAsync(scheduleId, ct);
        return Ok(new { ok = true });
    }
}
