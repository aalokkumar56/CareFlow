using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using System.Text.Json;
namespace CureFlow.Infrastructure.Areas.Platform;
public sealed class PlatformAccess(ICureFlowDbSession db)
{
 public async Task<(PlatformUser User, string[] Permissions)> RequireAsync(string? permission = null, ICureFlowDbSession? session = null)
 {
  var store = session ?? db;
  var user = db.UserId.HasValue ? await store.GetByIdAsync<PlatformUser>(db.UserId.Value, true) : null;
  if (user is null || !user.IsActive || user.IsDeleted) throw new DomainException("Your platform session is no longer active. Sign in again.", 401);
  var role = await store.QueryFirstOrDefaultAsync<PlatformRole>("SELECT * FROM \"PlatformRoles\" WHERE \"Key\" = @Role AND \"IsDeleted\" = false AND \"IsActive\" = true", new { user.Role }, true);
  var permissions = user.Role == "owner" ? PlatformPermissions.All : role is null ? [] : JsonSerializer.Deserialize<string[]>(role.PermissionsJson) ?? [];
  if (permission is not null && !PlatformPermissions.Can(user.Role, permissions, permission))
   throw new DomainException("You do not have permission to perform this platform action.", 403);
  return (user, permissions);
 }
}

