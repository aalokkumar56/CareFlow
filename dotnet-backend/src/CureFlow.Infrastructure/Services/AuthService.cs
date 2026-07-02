using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using Microsoft.Extensions.Logging;

namespace CureFlow.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ICureFlowDbSession _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;
    private readonly IUserPermissionService _permissions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(ICureFlowDbSession db, IPasswordHasher hasher, IJwtTokenService jwt,
        ITenantContext tenant, IAuditService audit, IUserPermissionService permissions, ILogger<AuthService> logger)
    {
        _db = db; _hasher = hasher; _jwt = jwt; _tenant = tenant; _audit = audit; _permissions = permissions; _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            """
            SELECT * FROM "Users"
            WHERE "Email" = @Email AND "IsActive" = true AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Email = req.Email.ToLower() },
            ignoreTenant: true,
            ct);

        if (user == null || !_hasher.Verify(req.Password, user.PasswordHash))
            throw new DomainException("Invalid email or password", 401);

        var tenant = await _db.QueryFirstOrDefaultAsync<Tenant>(
            """
            SELECT * FROM "Tenants"
            WHERE "Id" = @Id AND "IsActive" = true AND "IsDeleted" = false
            LIMIT 1
            """,
            new { Id = user.TenantId },
            ignoreTenant: true,
            ct);
        if (tenant == null) throw new DomainException("Hospital account inactive", 403);

        user.LastLoginAt = DateTime.UtcNow;
        await _permissions.AssignLegacyRoleAsync(user, ct);
        await _db.UpdateAsync(user, ignoreTenant: true, ct);

        var permissionCodes = await _permissions.GetPermissionCodesAsync(user, ct);
        var token = _jwt.Issue(user.Id, user.TenantId, user.Email, user.Role.ToString(), permissionCodes);
        return new AuthResponse(
            token,
            new UserDto(
                user.Id, user.Name, user.Email, user.Role, user.IsActive, user.Specialty, user.Phone,
                permissionCodes),
            new TenantDto(tenant.Id, tenant.Slug, tenant.Name, tenant.Plan, tenant.SubscriptionStatus));
    }

    public async Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest req, CancellationToken ct = default)
    {
        var slug = Slugify(req.HospitalName);
        var email = req.AdminEmail.ToLower();

        if (await _db.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Tenants" WHERE "Slug" = @Slug AND "IsDeleted" = false LIMIT 1""",
                new { Slug = slug },
                ignoreTenant: true,
                ct) != null)
            throw new ValidationException("Hospital with this name already exists. Pick a different name.");

        if (await _db.QueryFirstOrDefaultAsync<int?>(
                """SELECT 1 FROM "Users" WHERE "Email" = @Email AND "IsDeleted" = false LIMIT 1""",
                new { Email = email },
                ignoreTenant: true,
                ct) != null)
            throw new ValidationException("Email already registered.");

        var tenant = new Tenant
        {
            Slug = slug,
            Name = req.HospitalName,
            ContactName = req.AdminName,
            ContactEmail = email,
            ContactPhone = req.Phone,
            Plan = SubscriptionPlan.Trial,
            SubscriptionStatus = SubscriptionStatus.Trialing,
            TrialEndsAt = DateTime.UtcNow.AddDays(14),
        };

        var admin = new User
        {
            Name = req.AdminName,
            Email = email,
            PasswordHash = _hasher.Hash(req.AdminPassword),
            Role = UserRole.TenantOwner,
            Phone = req.Phone,
        };

        await _db.TransactionAsync(async session =>
        {
            await session.InsertAsync(tenant, ignoreTenant: true, ct);
            admin.TenantId = tenant.Id;
            await session.InsertAsync(admin, ignoreTenant: true, ct);
            await session.InsertAsync(new HospitalProfile { TenantId = tenant.Id, Name = req.HospitalName }, ignoreTenant: true, ct);
        }, ct);

        await _permissions.AssignLegacyRoleAsync(admin, ct);
        _logger.LogInformation("Registered tenant {slug}", tenant.Slug);
        return new TenantDto(tenant.Id, tenant.Slug, tenant.Name, tenant.Plan, tenant.SubscriptionStatus);
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
        return new UserDto(
            user.Id, user.Name, user.Email, user.Role, user.IsActive, user.Specialty, user.Phone,
            permissionCodes);
    }

    public async Task<UserDto> GetMeAsync(CancellationToken ct = default)
    {
        var user = await _db.GetByIdAsync<User>(_tenant.UserId!.Value, ct: ct)
            ?? throw new NotFoundException("User");
        var permissionCodes = await _permissions.GetPermissionCodesAsync(user, ct);
        return new UserDto(
            user.Id, user.Name, user.Email, user.Role, user.IsActive, user.Specialty, user.Phone,
            permissionCodes);
    }

    private static string Slugify(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
}
