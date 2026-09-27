using CureFlow.Application.Common;
using CureFlow.Infrastructure.Areas.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace CureFlow.Api.Areas.Platform.Controllers;
[ApiController]
[Route("api/tenant/modules")]
[Authorize]
public sealed class TenantModulesController(ITenantContext tenant,PlatformAdminService service):ControllerBase
{
 [HttpGet]
 public async Task<IActionResult> Get() {
  if(User.HasClaim("platform_user","true")||tenant.TenantId==Guid.Empty)return Forbid();
  return Ok(await service.TenantModules(tenant.TenantId));
 }
}

