using CureFlow.Application.Interfaces;
using CureFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskService _svc;
    public TasksController(ITaskService svc) => _svc = svc;

    public record CreateTaskRequest(string Title, TaskType Type, Guid? PatientId, string? Notes, DateTime? DueAt, Priority Priority, Guid? AssignedTo);

    [HttpPost]
    [Authorize(Policy = "Permission:Dashboard.View")]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest r, CancellationToken ct)
    { var id = await _svc.CreateAsync(r.Title, r.Type, r.PatientId, r.Notes, r.DueAt, r.Priority, r.AssignedTo, ct); return Ok(new { id }); }

    [HttpGet]
    [Authorize(Policy = "Permission:Dashboard.View")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) => Ok(await _svc.ListAsync(status, ct));

    public record UpdateTaskRequest(CureFlow.Domain.Enums.TaskStatus? Status);

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = "Permission:Dashboard.View")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskRequest r, CancellationToken ct)
    { await _svc.UpdateAsync(id, r.Status, ct); return Ok(new { ok = true }); }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:Dashboard.View")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await _svc.DeleteAsync(id, ct); return Ok(new { ok = true }); }
}
