using System.Security.Cryptography;
using System.Text;
using CureFlow.Infrastructure.Identity;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Infrastructure.Services;

public record RefreshResult(string AccessToken, string RefreshToken, DateTime RefreshExpiresAt);

public class RefreshSessionService(ICureFlowDbSession db, IJwtTokenService jwt, IUserPermissionService permissions)
{
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static Task<int> Insert(ICureFlowDbSession session, string token, Guid familyId, Guid userId, DateTime expiresAt, CancellationToken ct) =>
        session.ExecuteAsync("""
            INSERT INTO "RefreshSessions" ("TokenHash", "FamilyId", "UserId", "ExpiresAt", "Consumed", "Revoked")
            VALUES (@hash, @familyId, @userId, @expiresAt, false, false)
            """, new { hash = Hash(token), familyId, userId, expiresAt }, ignoreTenant: true, ct: ct);

    public async Task<RefreshResult> StartAsync(AuthResponse login, CancellationToken ct)
    {
        var token = NewToken();
        var expires = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()).UtcDateTime;
        await Insert(db, token, Guid.NewGuid(), login.User.Id, expires, ct);
        return new(login.AccessToken, token, expires);
    }

    public async Task<RefreshResult> RotateAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
            throw new DomainException("Invalid refresh session", 401);
        var known = await db.QueryFirstOrDefaultAsync<RefreshSession>(
            """SELECT * FROM "RefreshSessions" WHERE "TokenHash" = @hash""",
            new { hash = Hash(token) }, ignoreTenant: true, ct: ct);
        if (known == null) throw new DomainException("Invalid refresh session", 401);
        RefreshResult? result = null;
        await db.TransactionAsync(async session =>
        {
            // Serialize rotation, reuse detection and logout for this entire token family.
            await session.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@family, 0))",
                new { family = known.FamilyId.ToString() }, ignoreTenant: true, ct: ct);
            var row = await session.QueryFirstOrDefaultAsync<RefreshSession>(
                """SELECT * FROM "RefreshSessions" WHERE "TokenHash" = @hash FOR UPDATE""",
                new { hash = Hash(token) }, ignoreTenant: true, ct: ct);
            if (row == null) return;
            if (row.Consumed || row.Revoked || row.ExpiresAt <= DateTime.UtcNow)
            {
                await session.ExecuteAsync("""UPDATE "RefreshSessions" SET "Revoked" = true WHERE "FamilyId" = @familyId""",
                    new { familyId = row.FamilyId }, ignoreTenant: true, ct: ct);
                return;
            }
            var user = await session.QueryFirstOrDefaultAsync<User>(
                """SELECT * FROM "Users" WHERE "Id" = @id AND "IsActive" AND NOT "IsDeleted" """,
                new { id = row.UserId }, ignoreTenant: true, ct: ct);
            if (user == null) return;
            var active = await session.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Tenants" WHERE "Id" = @id AND "IsActive" AND NOT "IsDeleted" AND "LifecycleStatus" NOT IN (@rejected, @suspended)""",
                new { id = user.TenantId, rejected = (int)TenantLifecycleStatus.Rejected, suspended = (int)TenantLifecycleStatus.Suspended },
                ignoreTenant: true, ct: ct);
            if (active == null) return;
            var codes = await permissions.GetPermissionCodesAsync(user, ct);
            var access = jwt.Issue(user.Id, user.TenantId, user.Email, user.Role.ToString(), codes);
            var replacement = NewToken();
            await session.ExecuteAsync("""UPDATE "RefreshSessions" SET "Consumed" = true WHERE "TokenHash" = @hash""",
                new { hash = row.TokenHash }, ignoreTenant: true, ct: ct);
            await Insert(session, replacement, row.FamilyId, row.UserId, row.ExpiresAt, ct);
            result = new(access, replacement, row.ExpiresAt);
        }, ct);
        return result ?? throw new DomainException("Refresh session expired or revoked. Please sign in.", 401);
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var row = await db.QueryFirstOrDefaultAsync<RefreshSession>(
            """SELECT * FROM "RefreshSessions" WHERE "TokenHash" = @hash""",
            new { hash = Hash(token) }, ignoreTenant: true, ct: ct);
        if (row == null) return;
        await db.TransactionAsync(async session =>
        {
            await session.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@family, 0))",
                new { family = row.FamilyId.ToString() }, ignoreTenant: true, ct: ct);
            await session.ExecuteAsync("""UPDATE "RefreshSessions" SET "Revoked" = true WHERE "FamilyId" = @familyId""",
                new { familyId = row.FamilyId }, ignoreTenant: true, ct: ct);
        }, ct);
    }
}
