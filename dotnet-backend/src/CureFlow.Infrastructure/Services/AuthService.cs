using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Seeders;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ICureFlowDbSession _db;
    private readonly IIdentityDbSession _identity;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly IUserPermissionService _permissions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ICureFlowDbSession db, IIdentityDbSession identity, IPasswordHasher hasher, IJwtTokenService jwt,
        ITenantContext tenant, IAuditService audit, IUserPermissionService permissions, ILogger<AuthService> logger)
    {
        _db = db; _identity = identity; _hasher = hasher; _jwt = jwt; _tenant = tenant; _audit = audit; _permissions = permissions; _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        AuthResponse? result = null;
        await _identity.TransactionAsync(async store =>
        {
            var matchingUsers = await store.QueryAsync<User>("""
                SELECT u.* FROM "Users" u JOIN "Tenants" t ON t."Id"=u."TenantId"
                WHERE u."Email"=@email AND u."IsActive" AND NOT u."IsDeleted" AND NOT t."IsDeleted"
                  AND (@slug IS NULL OR t."Slug"=@slug)
                ORDER BY u."Id" LIMIT 2 FOR UPDATE OF u
                """, new { email = req.Email.ToLowerInvariant(),
                    slug = string.IsNullOrWhiteSpace(req.TenantSlug) ? null : req.TenantSlug.Trim().ToLowerInvariant() },
                ignoreTenant: true, ct: ct);
            var user = matchingUsers.Count == 1 ? matchingUsers[0] : null;
            if (user is null || !_hasher.Verify(req.Password, user.PasswordHash))
                throw new DomainException("Invalid email or password", 401);
            var hospital = await store.QueryFirstOrDefaultAsync<Tenant>("""
                SELECT * FROM "Tenants" WHERE "Id"=@id AND NOT "IsDeleted"
                """, new { id = user.TenantId }, ignoreTenant: true, ct: ct)
                ?? throw new DomainException("Hospital account unavailable", 403);
            if (!hospital.IsActive || hospital.LifecycleStatus is TenantLifecycleStatus.Rejected or TenantLifecycleStatus.Suspended)
                throw new DomainException("Hospital account unavailable. Contact support.", 403);
            await _permissions.AssignLegacyRoleAsync(user, ct, store);
            // Update only login metadata; never overwrite a concurrent security field with a stale entity.
            await store.ExecuteAsync("""
                UPDATE "Users" SET "LastLoginAt"=CURRENT_TIMESTAMP,"UpdatedAt"=CURRENT_TIMESTAMP WHERE "Id"=@id
                """, new { id = user.Id }, ignoreTenant: true, ct: ct);
            user = await store.QueryFirstOrDefaultAsync<User>("""SELECT * FROM "Users" WHERE "Id"=@id""",
                new { id = user.Id }, ignoreTenant: true, ct: ct) ?? throw new DomainException("Session unavailable", 401);
            var codes = await _permissions.GetPermissionCodesAsync(user, ct, store);
            // Only StartAsync creates a persisted family and issues an externally usable access token.
            result = new AuthResponse(string.Empty, MapUser(user, codes), TenantMapping.ToDto(hospital))
                { ValidatedSecurityRevision = user.SecurityRevision };
        }, ct, ignoreTenant: true);
        return result!;
    }
    public async Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest req, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(req.TenantType)) throw new ValidationException("Select a supported organization type.");
        var slug = await ResolveUniqueSlugAsync(req.HospitalName, ct);
        var email = req.AdminEmail.ToLower();

        if (await _identity.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Users" WHERE "Email" = @Email AND "IsDeleted" = false LIMIT 1""",
                new { Email = email },
                ignoreTenant: true,
                ct) != null)
            throw new ValidationException("Email already registered.");

        var tenant = new Tenant
        {
            Slug = slug,
            Name = req.HospitalName,
            TenantType = req.TenantType,
            ContactName = req.AdminName,
            ContactEmail = email,
            ContactPhone = req.Phone,
            Plan = SubscriptionPlan.Trial,
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(14),
            LifecycleStatus = TenantLifecycleStatus.PendingApproval,
            SubmittedAt = DateTime.UtcNow,
            OnboardingComplete = false,
            IsActive = true,
        };

        var admin = new User
        {
            Name = req.AdminName,
            Email = email,
            PasswordHash = _hasher.Hash(req.AdminPassword),
            Role = UserRole.TenantOwner,
            Phone = req.Phone,
        };

        await _identity.TransactionAsync(async session =>
        {
            await session.InsertAsync(tenant, ignoreTenant: true, ct);
            admin.TenantId = tenant.Id;
            await session.InsertAsync(admin, ignoreTenant: true, ct);
            await session.InsertAsync(new HospitalProfile { TenantId = tenant.Id, Name = req.HospitalName }, ignoreTenant: true, ct);
            await session.InsertAsync(new TenantOnboardingState { TenantId = tenant.Id }, ignoreTenant: true, ct);
            await _permissions.AssignLegacyRoleAsync(admin, ct, session);
            await TemplatePlaceholderSeeder.SeedForTenantAsync(session, tenant, ct);
        }, ct, ignoreTenant: true);



        _logger.LogInformation("Registered tenant {slug} pending approval", tenant.Slug);
        return TenantMapping.ToDto(tenant);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest req, CancellationToken ct = default)
    {
        if (!_tenant.IsAuthenticated) throw new ForbiddenException();

        if (await _db.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Users" WHERE "Email" = @Email LIMIT 1""",
                new { Email = req.Email.ToLower() },
                ct: ct) != null)
            throw new ValidationException("Email already exists in this hospital");

        if (!EnumParseHelper.TryParseSnakeCase<UserRole>(req.Role, out var role))
            throw new ValidationException("Invalid role");

        var user = new User
        {
            Name = req.Name,
            Email = req.Email.ToLower(),
            PasswordHash = _hasher.Hash(req.Password),
            Role = role,
            Specialty = req.Specialty,
            Phone = req.Phone,
            Qualifications = req.Qualifications,
        };
        await _db.InsertAsync(user, ct: ct);
        await _permissions.AssignLegacyRoleAsync(user, ct);
        await _audit.LogAsync("user.create", "user", user.Id.ToString(), new { req.Email, req.Role }, ct);
        var permissionCodes = await _permissions.GetPermissionCodesAsync(user, ct);
        return MapUser(user, permissionCodes);
    }

    public async Task<UserDto> GetMeAsync(CancellationToken ct = default)
    {
        var user = await _db.GetByIdAsync<User>(_tenant.UserId!.Value, ct: ct)
            ?? throw new NotFoundException("User");
        var permissionCodes = await _permissions.GetPermissionCodesAsync(user, ct);
        return MapUser(user, permissionCodes);
    }

    public async Task<SessionDto> GetSessionAsync(CancellationToken ct = default)
    {
        var user = await _db.GetByIdAsync<User>(_tenant.UserId!.Value, ct: ct)
            ?? throw new NotFoundException("User");
        var tenant = await _db.QueryFirstOrDefaultAsync<Tenant>(
            """
            SELECT * FROM "Tenants"
            WHERE "Id" = @Id AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Id = user.TenantId },
            ignoreTenant: true,
            ct);
        if (tenant == null) throw new NotFoundException("Tenant");

        var permissionCodes = await _permissions.GetPermissionCodesAsync(user, ct);
        return new SessionDto(MapUser(user, permissionCodes), TenantMapping.ToDto(tenant));
    }

    private static UserDto MapUser(User user, IReadOnlyList<string> permissionCodes) =>
        new(user.Id, user.Name, user.Email, user.Role, user.IsActive, user.Specialty, user.Phone, permissionCodes);

    private async Task<string> ResolveUniqueSlugAsync(string hospitalName, CancellationToken ct)
    {
        var baseSlug = TenantSlugHelper.Slugify(hospitalName);
        if (string.IsNullOrWhiteSpace(baseSlug))
            throw new ValidationException("Hospital name must contain at least one letter or number.");

        if (TenantSlugHelper.IsReserved(baseSlug))
            throw new ValidationException($"Hospital name produces a reserved slug '{baseSlug}'. Pick a different name.");

        for (var attempt = 1; attempt < 100; attempt++)
        {
            var candidate = TenantSlugHelper.WithSuffix(baseSlug, attempt);
            var taken = await _identity.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Tenants" WHERE "Slug" = @Slug AND "IsDeleted" = false LIMIT 1""",
                new { Slug = candidate },
                ignoreTenant: true,
                ct);
            if (taken == null)
                return candidate;
        }

        throw new ValidationException("Could not generate a unique hospital slug. Try a different name.");
    }
}





