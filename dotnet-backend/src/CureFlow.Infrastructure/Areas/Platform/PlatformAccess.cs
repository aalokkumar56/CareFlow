using CureFlow.Application.Areas.Platform;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using System.Text.Json;
namespace CureFlow.Infrastructure.Areas.Platform;
public sealed class PlatformAccess(IPlatformDbSession db, ITenantContext? tenant = null)
{
 public async Task<(PlatformUser User, string[] Permissions)> RequireAsync(string? permission = null, ICureFlowDbSession? session = null)
 {
  if (tenant is null || !tenant.IsAuthenticated || !tenant.IsPlatformUser || tenant.TenantId != Guid.Empty
      || !tenant.UserId.HasValue || tenant.UserId.Value == Guid.Empty)
   throw new DomainException("Authenticated platform authority is required.", 403);
  var store = session ?? db;
  if (!tenant.PlatformSessionId.HasValue) throw new DomainException("Sign in again.", 401);
  var activeSession = await store.QueryFirstOrDefaultAsync<PlatformSession>(
      "SELECT \"Id\",\"PlatformUserId\",\"ExpiresAt\",\"RevokedAt\",\"SecurityRevision\",\"IsActive\",\"IsDeleted\" FROM \"PlatformSessions\" WHERE \"Id\"=@Id AND \"PlatformUserId\"=@UserId AND \"RevokedAt\" IS NULL AND \"ExpiresAt\">CURRENT_TIMESTAMP AND \"SecurityRevision\"=@Revision AND NOT \"IsDeleted\" AND \"IsActive\"",
      new { Id = tenant.PlatformSessionId.Value, UserId = tenant.UserId.Value, Revision = tenant.PlatformSessionRevision }, true);
  if (activeSession is null) throw new DomainException("Your platform session has expired or was revoked. Sign in again.", 401);
  var user = db.UserId.HasValue ? await store.GetByIdAsync<PlatformUser>(db.UserId.Value, true) : null;
  if (user is null || !user.IsActive || user.IsDeleted) throw new DomainException("Your platform session is no longer active. Sign in again.", 401);
  var role = await store.QueryFirstOrDefaultAsync<PlatformRole>("SELECT * FROM \"PlatformRoles\" WHERE \"Key\" = @Role AND \"IsDeleted\" = false AND \"IsActive\" = true", new { user.Role }, true);
  var permissions = user.Role == "owner" ? PlatformPermissions.All : role is null ? [] : JsonSerializer.Deserialize<string[]>(role.PermissionsJson) ?? [];
  if (permission is not null && !PlatformPermissions.Can(user.Role, permissions, permission))
   throw new DomainException("You do not have permission to perform this platform action.", 403);
  return (user, permissions);
 }
}

