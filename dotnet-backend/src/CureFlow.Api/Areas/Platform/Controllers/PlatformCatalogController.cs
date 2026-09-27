using CureFlow.Application.Areas.Platform;
using CureFlow.Infrastructure.Areas.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CureFlow.Api.Areas.Platform;
namespace CureFlow.Api.Areas.Platform.Controllers;
[ApiController]
[Area("Platform")]
[Route("api/platform")]
[Authorize(Policy="PlatformUser")]
public class PlatformCatalogController(PlatformAdminService service) : ControllerBase
{
 private string Key => Request.Headers["X-Request-ID"].ToString();
 [HttpGet("plans")]
 [PlatformPermission("billing.read")]
 public async Task<IActionResult> Plans() => Ok(await service.PlanModules());
 [HttpPut("plans/{plan}/modules/{module:guid}")]
 [PlatformPermission("billing.write")]
 public async Task<IActionResult> UpdatePlanModule(CureFlow.Domain.Enums.SubscriptionPlan plan,Guid module,[FromBody] PlanModuleInput input) => Ok(await service.PlanModule(plan,module,input,Key));
 [HttpGet("me")]
 
 public async Task<IActionResult> Action0() => Ok(await service.Me());

 [HttpGet("overview")]
 [PlatformPermission("overview.read")]
 public async Task<IActionResult> Action1() => Ok(await service.Overview());

 [HttpGet("modules")]
 [PlatformPermission("modules.read")]
 public async Task<IActionResult> Action2() => Ok(await service.Modules());

 [HttpPost("modules")]
 [PlatformPermission("modules.write")]
 public async Task<IActionResult> Action3([FromBody] ModuleInput input) => Ok(await service.Module(null,input,Key));

 [HttpPatch("modules/{id:guid}")]
 [PlatformPermission("modules.write")]
 public async Task<IActionResult> Action4(Guid id,[FromBody] ModuleInput input) => Ok(await service.Module(id,input,Key));

 [HttpGet("roles")]
 [PlatformPermission("roles.read")]
 public async Task<IActionResult> Action5() => Ok(await service.Roles());

 [HttpGet("permissions")]
 [PlatformPermission("roles.read")]
 public IActionResult Action6() => Ok(PlatformPermissions.All);

 [HttpPost("roles")]
 [PlatformPermission("roles.write")]
 public async Task<IActionResult> Action7([FromBody] RoleInput input) => Ok(await service.Role(null,input,Key));

 [HttpPatch("roles/{id:guid}")]
 [PlatformPermission("roles.write")]
 public async Task<IActionResult> Action8(Guid id,[FromBody] RoleInput input) => Ok(await service.Role(id,input,Key));

 [HttpGet("operators")]
 [PlatformPermission("operators.read")]
 public async Task<IActionResult> Action9() => Ok(await service.Operators());

 [HttpPost("operators")]
 [PlatformPermission("operators.write")]
 public async Task<IActionResult> Action10([FromBody] OperatorInput input) => Ok(await service.Operator(null,input,Key));

 [HttpPatch("operators/{id:guid}")]
 [PlatformPermission("operators.write")]
 public async Task<IActionResult> Action11(Guid id,[FromBody] OperatorInput input) => Ok(await service.Operator(id,input,Key));

 [HttpGet("audit-logs")]
 [PlatformPermission("audit.read")]
 public async Task<IActionResult> Action12([FromQuery] string? search=null,[FromQuery] Guid? tenantId=null,[FromQuery] int page=1) => Ok(await service.Audit(search,tenantId,page));

 [HttpGet("operations/jobs")]
 [PlatformPermission("operations.read")]
 public async Task<IActionResult> Action13() => Ok(await service.Jobs());

 [HttpPost("operations/jobs")]
 [PlatformPermission("operations.write")]
 public async Task<IActionResult> Action14([FromBody] JobInput input) => Ok(await service.Provision(input,Key));

 [HttpGet("support-sessions")]
 [PlatformPermission("support.read")]
 public async Task<IActionResult> Action15() => Ok(await service.SupportSessions());

 [HttpPost("support-sessions")]
 [PlatformPermission("support.write")]
 public async Task<IActionResult> Action16([FromBody] SupportInput input) => Ok(await service.Support(input,Key));

 [HttpPatch("support-sessions/{id:guid}/revoke")]
 [PlatformPermission("support.write")]
 public async Task<IActionResult> Action17(Guid id,[FromBody] PlatformChange input) => Ok(await service.Revoke(id,input,Key));

 [HttpGet("support-sessions/{id:guid}/snapshot")]
 [PlatformPermission("support.read")]
 public async Task<IActionResult> Action18(Guid id) => Ok(await service.SupportSnapshot(id));


}
