using System.Security.Cryptography;
using System.Text;
using CureFlow.Infrastructure.Identity;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Enums;

namespace CureFlow.Infrastructure.Services;

public record RefreshResult(string AccessToken, string RefreshToken, DateTime RefreshExpiresAt, string RecoveryMode = "single_use_reauthenticate_on_ambiguity");

public class RefreshSessionService(IIdentityDbSession db, IJwtTokenService jwt, IUserPermissionService permissions)
{
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static Task<int> Insert(ICureFlowDbSession session, string token, Guid familyId, Guid userId, int revision, DateTime expiresAt, CancellationToken ct) =>
        session.ExecuteAsync("""
            INSERT INTO "RefreshSessions" ("TokenHash", "FamilyId", "UserId", "ExpiresAt", "Consumed", "Revoked", "SecurityRevision")
            VALUES (@hash, @familyId, @userId, @expiresAt, false, false, @revision)
            """, new { hash = Hash(token), familyId, userId, revision, expiresAt }, ignoreTenant: true, ct: ct);

    public async Task<RefreshResult> StartAsync(AuthResponse login, CancellationToken ct)
    {
        var token = NewToken();
        var expires = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()).UtcDateTime;
        RefreshResult? result = null;
        await db.TransactionAsync(async store =>
        {
            var user = await store.QueryFirstOrDefaultAsync<User>(
                """SELECT * FROM "Users" WHERE "Id"=@id AND "IsActive" AND NOT "IsDeleted" FOR SHARE""",
                new { id = login.User.Id }, ignoreTenant: true, ct: ct) ?? throw new DomainException("Session unavailable", 401);
            if (login.ValidatedSecurityRevision < 1 || login.ValidatedSecurityRevision != user.SecurityRevision)
                throw new DomainException("Security state changed. Please sign in again.", 401);
            var family = Guid.NewGuid();
            var codes = await permissions.GetPermissionCodesAsync(user, ct, store);
            await Insert(store, token, family, user.Id, user.SecurityRevision, expires, ct);
            result = new(jwt.Issue(user.Id, user.TenantId, user.Email, user.Role.ToString(), codes,
                sessionFamilyId: family, securityRevision: user.SecurityRevision), token, expires);
        }, ct, ignoreTenant: true);
        return result!;
    }

    public async Task<RefreshResult> RotateAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64 || !token.All(Uri.IsHexDigit))
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
            if (user == null || user.SecurityRevision != row.SecurityRevision) return;
            var active = await session.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Tenants" WHERE "Id" = @id AND "IsActive" AND NOT "IsDeleted" AND "LifecycleStatus" NOT IN (@rejected, @suspended)""",
                new { id = user.TenantId, rejected = (int)TenantLifecycleStatus.Rejected, suspended = (int)TenantLifecycleStatus.Suspended },
                ignoreTenant: true, ct: ct);
            if (active == null) return;
            var codes = await permissions.GetPermissionCodesAsync(user, ct, session);
            var access = jwt.Issue(user.Id, user.TenantId, user.Email, user.Role.ToString(), codes, sessionFamilyId: row.FamilyId, securityRevision: user.SecurityRevision);
            var replacement = NewToken();
            await session.ExecuteAsync("""UPDATE "RefreshSessions" SET "Consumed" = true WHERE "TokenHash" = @hash""",
                new { hash = row.TokenHash }, ignoreTenant: true, ct: ct);
            await Insert(session, replacement, row.FamilyId, row.UserId, user.SecurityRevision, row.ExpiresAt, ct);
            result = new(access, replacement, row.ExpiresAt);
        }, ct, ignoreTenant: true);
        return result ?? throw new DomainException("Refresh session expired or revoked. Please sign in.", 401);
    }

    public async Task<User> RequireAccessAsync(Guid family, int revision, Guid userId, Guid tenantId, CancellationToken ct)
    {
        User? result = null;
        await db.TransactionAsync(async store =>
        {
            var user = await store.QueryFirstOrDefaultAsync<User>(
                """SELECT * FROM "Users" WHERE "Id"=@userId AND "TenantId"=@tenantId AND "IsActive" AND NOT "IsDeleted" """,
                new { userId, tenantId }, ignoreTenant: true, ct: ct);
            if (user is null || revision < 1 || user.SecurityRevision != revision) return;
            var active = await store.QueryFirstOrDefaultAsync<int?>("""
                SELECT 1 FROM "RefreshSessions" s JOIN "Tenants" t ON t."Id"=@tenantId
                WHERE s."FamilyId"=@family AND s."UserId"=@userId AND s."SecurityRevision"=@revision
                  AND NOT s."Revoked" AND NOT s."Consumed" AND s."ExpiresAt">CURRENT_TIMESTAMP
                  AND t."IsActive" AND NOT t."IsDeleted" AND t."LifecycleStatus" NOT IN (@rejected,@suspended) LIMIT 1
                """, new { family, userId, tenantId, revision, rejected=(int)TenantLifecycleStatus.Rejected,
                    suspended=(int)TenantLifecycleStatus.Suspended }, ignoreTenant: true, ct: ct);
            if (active.HasValue) result = user;
        }, ct, ignoreTenant: true);
        return result ?? throw new DomainException("Session expired or revoked. Please sign in.", 401);
    }
    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) throw new DomainException("Invalid refresh session", 400);
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
        }, ct, ignoreTenant: true);
    }
}



