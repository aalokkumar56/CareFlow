using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditLogsController(IAuditService audit) => _audit = audit;

    [HttpGet]
    [Authorize(Policy = "Permission:Audit.View")]
    public async Task<IActionResult> List([FromQuery] int limit = 30, CancellationToken ct = default) =>
        Ok(await _audit.ListAsync(limit, ct));
}
