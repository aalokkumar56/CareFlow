using CureFlow.Application.Areas.Platform;
using CureFlow.Infrastructure.Areas.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CureFlow.Api.Areas.Platform;
namespace CureFlow.Api.Areas.Platform.Controllers;
[ApiController]
[Area("Platform")]
[Route("api/platform/tenants")]
[Authorize(Policy="PlatformUser")]
public class PlatformTenantsController(PlatformAdminService service) : ControllerBase
{
 private string Key => Request.Headers["X-Request-ID"].ToString();
 [HttpDelete("{id:guid}/entitlements/{module:guid}")]
 [PlatformPermission("entitlements.write")]
 public async Task<IActionResult> InheritPlan(Guid id,Guid module,[FromBody] PlatformChange input) => Ok(await service.ResetEntitlement(id,module,input,Key));
 [HttpPatch("{id:guid}/profile")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> UpdateProfile(Guid id,[FromBody] TenantProfileInput input) => Ok(await service.Profile(id,input,Key));
 [HttpGet("")]
 [PlatformPermission("tenants.read")]
 public async Task<IActionResult> Action0() => Ok(await service.Tenants());

 [HttpGet("{id:guid}")]
 [PlatformPermission("tenants.read")]
 public async Task<IActionResult> Action1(Guid id) => Ok(await service.TenantDetail(id));

 [HttpPatch("{id:guid}/lifecycle/{action}")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action2(Guid id,string action,[FromBody] PlatformChange input) => Ok(await service.Lifecycle(id,action,input,Key));

 [HttpPatch("{id:guid}/approve")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action3(Guid id,[FromBody] PlatformChange input) => Ok(await service.Lifecycle(id,"approve",input,Key));

 [HttpPatch("{id:guid}/reject")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action4(Guid id,[FromBody] PlatformChange input) => Ok(await service.Lifecycle(id,"reject",input,Key));

 [HttpPatch("{id:guid}/suspend")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action5(Guid id,[FromBody] PlatformChange input) => Ok(await service.Lifecycle(id,"suspend",input,Key));

 [HttpPatch("{id:guid}/activate")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action6(Guid id,[FromBody] PlatformChange input) => Ok(await service.Lifecycle(id,"activate",input,Key));

 [HttpPatch("{id:guid}/reopen")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action7(Guid id,[FromBody] PlatformChange input) => Ok(await service.Lifecycle(id,"reopen",input,Key));

 [HttpPatch("{id:guid}/type")]
 [PlatformPermission("tenants.write")]
 public async Task<IActionResult> Action8(Guid id,[FromBody] TenantTypeInput input) => Ok(await service.Type(id,input,Key));

 [HttpPatch("{id:guid}/plan")]
 [PlatformPermission("billing.write")]
 public async Task<IActionResult> Action9(Guid id,[FromBody] SubscriptionInput input) => Ok(await service.Plan(id,input,Key));

 [HttpGet("{id:guid}/facilities")]
 [PlatformPermission("facilities.read")]
 public async Task<IActionResult> Action10(Guid id) => Ok(await service.Facilities(id));

 [HttpPost("{id:guid}/facilities")]
 [PlatformPermission("facilities.write")]
 public async Task<IActionResult> Action11(Guid id,[FromBody] FacilityInput input) => Ok(await service.Facility(id,null,input,Key));

 [HttpPatch("{id:guid}/facilities/{facility:guid}")]
 [PlatformPermission("facilities.write")]
 public async Task<IActionResult> Action12(Guid id,Guid facility,[FromBody] FacilityInput input) => Ok(await service.Facility(id,facility,input,Key));

 [HttpGet("{id:guid}/entitlements")]
 [PlatformPermission("entitlements.read")]
 public async Task<IActionResult> Action13(Guid id) => Ok(await service.Entitlements(id));

 [HttpPatch("{id:guid}/entitlements/{module:guid}")]
 [PlatformPermission("entitlements.write")]
 public async Task<IActionResult> Action14(Guid id,Guid module,[FromBody] EntitlementInput input) => Ok(await service.Entitlement(id,module,input,Key));


}
