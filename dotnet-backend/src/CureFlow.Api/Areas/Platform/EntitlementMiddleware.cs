using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Infrastructure.Areas.Platform;
namespace CureFlow.Api.Areas.Platform;
public sealed class EntitlementMiddleware(RequestDelegate next)
{
 public async Task InvokeAsync(HttpContext context, ICureFlowDbSession db, ITenantContext tenant)
 {
  if(tenant.TenantId!=Guid.Empty && context.User.Identity?.IsAuthenticated==true && !context.User.HasClaim("platform_user","true")) {
   var required=SupportedModules.RequiredForApi(context.Request.Path.Value??"");
   if(required.Length>0) {
    var modules=await PlatformModuleAccess.ResolveAsync(db,tenant.TenantId);
    if(required.Any(key=>!modules.Any(m=>m.Key==key&&m.Effective)))
     throw new DomainException("This module is not enabled for your account. Contact your administrator.",403);
   }
  }
  await next(context);
 }
}
