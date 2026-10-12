using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Infrastructure.Areas.Platform;

namespace CureFlow.Infrastructure.Services;

public class PlatformAuthService(IPlatformIdentityDbSession identity, IPlatformDbSession platform,
    PlatformAccess access, ITenantContext tenant, IPasswordHasher hasher, IJwtTokenService jwt) : IPlatformAuthService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
    public async Task<PlatformSetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default) => new(await CountUsers(identity, ct) == 0);

    public async Task<PlatformAuthResponse> BootstrapOwnerAsync(PlatformBootstrapRequest req, CancellationToken ct = default)
    {
        var name = req.Name?.Trim() ?? string.Empty;
        var email = req.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var password = req.Password ?? string.Empty;
        if (name.Length < 2 || name.Length > 200) throw new ValidationException("Name must contain 2 to 200 characters.");
        if (email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email, out _)) throw new ValidationException("A valid email is required.");
        if (password.Length < 8 || password.Length > 1024) throw new ValidationException("Password must contain 8 to 1024 characters.");
        PlatformAuthResponse? response = null;
        await identity.TransactionAsync(async store =>
        {
            await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734292)", ignoreTenant: true, ct: ct);
            if (await CountUsers(store, ct) != 0) throw new ConflictException("Platform owner already exists. Use sign-in instead.");
            var user = new PlatformUser { Name = name, Email = email, PasswordHash = hasher.Hash(password), Role = "owner" };
            await store.InsertAsync(user, ignoreTenant: true, ct: ct);
            response = await StartSession(store, user, "auth.bootstrap", ct);
        }, ct, ignoreTenant: true);
        return response!;
    }

    public async Task<PlatformAuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        PlatformAuthResponse? response = null;
        await identity.TransactionAsync(async store =>
        {
            // Serialize credential validation and issuance with operator/role changes.
            await store.ExecuteAsync("SELECT pg_advisory_xact_lock(734291)", ignoreTenant: true, ct: ct);
            var user = await store.QueryFirstOrDefaultAsync<PlatformUser>(
                """
                SELECT * FROM "PlatformUsers" WHERE LOWER("Email")=@Email
                    AND "IsActive" AND NOT "IsDeleted" LIMIT 1 FOR SHARE
                """,
                new { Email = req.Email.Trim().ToLowerInvariant() }, ignoreTenant: true, ct: ct);
            if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
                throw new DomainException("Invalid email or password", 401);
            response = await StartSession(store, user, "auth.login", ct);
        }, ct, ignoreTenant: true);
        return response!;
    }

    public async Task<PlatformSessionResponse> GetSessionAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var (user, permissions) = await access.RequireAsync();
        var session = await platform.QueryFirstOrDefaultAsync<PlatformSession>(
            """SELECT "ExpiresAt" FROM "PlatformSessions" WHERE "Id"=@Id AND "PlatformUserId"=@UserId AND "RevokedAt" IS NULL AND "ExpiresAt">CURRENT_TIMESTAMP""",
            new { Id = tenant.PlatformSessionId!.Value, UserId = user.Id }, true, ct);
        if (session is null) throw new DomainException("Sign in again.", 401);
        return new(user.Id, user.Email, user.Name, user.Role, permissions, session.ExpiresAt);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!tenant.IsAuthenticated || !tenant.IsPlatformUser || !tenant.UserId.HasValue || !tenant.PlatformSessionId.HasValue)
            throw new DomainException("Sign in again.", 401);
        await platform.TransactionAsync(async store =>
        {
            var affected = await store.ExecuteAsync(
                "UPDATE \"PlatformSessions\" SET \"RevokedAt\"=CURRENT_TIMESTAMP, \"UpdatedAt\"=CURRENT_TIMESTAMP, \"SecurityRevision\"=\"SecurityRevision\"+1 WHERE \"Id\"=@Id AND \"PlatformUserId\"=@UserId AND \"RevokedAt\" IS NULL",
                new { Id = tenant.PlatformSessionId.Value, UserId = tenant.UserId.Value }, true, ct);
            if (affected != 0)
                await store.InsertAsync(new PlatformAuditLog { PlatformUserId = tenant.UserId, Action = "auth.logout", MetadataJson = "{}" }, true, ct);
        }, ct, ignoreTenant: true);
    }

    private async Task<PlatformAuthResponse> StartSession(ICureFlowDbSession store, PlatformUser user, string action, CancellationToken ct)
    {
        var session = new PlatformSession { PlatformUserId = user.Id, ExpiresAt = DateTime.UtcNow.Add(SessionLifetime) };
        await store.InsertAsync(session, true, ct);
        await store.InsertAsync(new PlatformAuditLog { PlatformUserId = user.Id, Action = action, MetadataJson = "{}" }, true, ct);
        return new(jwt.IssuePlatformUser(user.Id, user.Email, SessionLifetime, session.Id, session.SecurityRevision), user.Email, user.Name);
    }
    private static Task<long> CountUsers(ICureFlowDbSession store, CancellationToken ct) => store.QuerySingleAsync<long>("SELECT COUNT(*) FROM \"PlatformUsers\"", ignoreTenant: true, ct: ct);
}
