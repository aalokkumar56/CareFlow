using CureFlow.Infrastructure.Areas.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Areas.Platform.Controllers;

[ApiController]
[Area("Platform")]
[Route("api/platform/api-activity")]
[Authorize(Policy = "PlatformUser")]
public sealed class PlatformApiActivityController(TenantApiActivityService service) : ControllerBase
{
    [HttpGet]
    [PlatformPermission("audit.read")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId = null, [FromQuery] string? search = null,
        [FromQuery] int? statusCode = null, [FromQuery] bool errorsOnly = false,
        [FromQuery] string? method = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await service.List(tenantId, search, statusCode, errorsOnly, method, from, to, page, pageSize));
}
