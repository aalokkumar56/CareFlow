using CureFlow.Infrastructure.Areas.Platform;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace CureFlow.Api.Areas.Platform;
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PlatformPermissionAttribute(string permission) : TypeFilterAttribute(typeof(PlatformPermissionFilter))
{
 public string Permission { get; } = permission;
 public override string ToString() => Permission;
}
public sealed class PlatformPermissionFilter(PlatformAccess access) : IAsyncActionFilter
{
 public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
 {
  var permission = context.ActionDescriptor.EndpointMetadata.OfType<PlatformPermissionAttribute>().LastOrDefault()?.Permission;
  await access.RequireAsync(permission);
  await next();
 }
}

