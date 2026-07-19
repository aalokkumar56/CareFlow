using System.IdentityModel.Tokens.Jwt;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Domain.Entities.Saas;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Unit tests for <see cref="AuthService"/> (controllers are thin wrappers).
/// HTTP-ready cases that need the API host belong under Integration with WebApplicationFactory —
/// see <see cref="AuthHttpIntegrationCases"/> below.
/// </summary>
public class AuthServiceOrControllerTests
{
    private readonly Mock<ICureFlowDbSession> _db = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IUserPermissionService> _permissions = new();
    private readonly JwtTokenService _jwt;

    public AuthServiceOrControllerTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "unit-test-secret-key-min-32-chars!!",
                ["Jwt:Issuer"] = "cureflow-test",
                ["Jwt:Audience"] = "cureflow-api-test",
            })
            .Build();
        _jwt = new JwtTokenService(config);
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
        _permissions.Setup(p => p.AssignLegacyRoleAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _permissions.Setup(p => p.GetPermissionCodesAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CureFlowPermissions.PatientView, CureFlowPermissions.UserCreate });
        _db.Setup(d => d.UpdateAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private AuthService CreateSut() => new(
        _db.Object,
        _hasher.Object,
        _jwt,
        _tenant.Object,
        _audit.Object,
        _permissions.Object,
        NullLogger<AuthService>.Instance);

    private static User ActiveUser(Guid tenantId, string email = "admin@hospital.test") => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        Name = "Admin",
        Email = email,
        PasswordHash = "stored-hash",
        Role = UserRole.TenantOwner,
        IsActive = true,
        IsDeleted = false,
    };

    private static Tenant ActiveTenant(Guid id) => new()
    {
        Id = id,
        Slug = "alpha-care",
        Name = "Alpha Care",
        LifecycleStatus = TenantLifecycleStatus.Active,
        IsActive = true,
        IsDeleted = false,
        Plan = SubscriptionPlan.Trial,
        SubscriptionStatus = SubscriptionStatus.Trialing,
        Timezone = "Asia/Kolkata",
    };

    [Fact]
    public async Task LoginAsync_rejects_unknown_user()
    {
        _db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var act = () => CreateSut().LoginAsync(new LoginRequest("missing@test.com", "pw"));
        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task LoginAsync_rejects_bad_password()
    {
        var tenantId = Guid.NewGuid();
        _db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveUser(tenantId));
        _hasher.Setup(h => h.Verify("wrong", "stored-hash")).Returns(false);

        var act = () => CreateSut().LoginAsync(new LoginRequest("admin@hospital.test", "wrong"));
        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(401);
        ex.Which.Message.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task LoginAsync_rejects_missing_tenant()
    {
        var tenantId = Guid.NewGuid();
        _db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveUser(tenantId));
        _hasher.Setup(h => h.Verify("ok", "stored-hash")).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<Tenant>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var act = () => CreateSut().LoginAsync(new LoginRequest("admin@hospital.test", "ok"));
        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(403);
        ex.Which.Message.Should().Contain("Hospital account not found");
    }

    [Fact]
    public async Task LoginAsync_rejects_rejected_tenant_with_reason()
    {
        var tenantId = Guid.NewGuid();
        var tenant = ActiveTenant(tenantId);
        tenant.LifecycleStatus = TenantLifecycleStatus.Rejected;
        tenant.RejectionReason = "Incomplete paperwork";

        _db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveUser(tenantId));
        _hasher.Setup(h => h.Verify("ok", "stored-hash")).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<Tenant>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var act = () => CreateSut().LoginAsync(new LoginRequest("admin@hospital.test", "ok"));
        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(403);
        ex.Which.Message.Should().Contain("Incomplete paperwork");
    }

    [Theory]
    [InlineData(TenantLifecycleStatus.Suspended, true)]
    [InlineData(TenantLifecycleStatus.Active, false)]
    public async Task LoginAsync_rejects_suspended_or_inactive_tenant(TenantLifecycleStatus lifecycle, bool isActive)
    {
        var tenantId = Guid.NewGuid();
        var tenant = ActiveTenant(tenantId);
        tenant.LifecycleStatus = lifecycle;
        tenant.IsActive = isActive;

        _db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveUser(tenantId));
        _hasher.Setup(h => h.Verify("ok", "stored-hash")).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<Tenant>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var act = () => CreateSut().LoginAsync(new LoginRequest("admin@hospital.test", "ok"));
        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.StatusCode.Should().Be(403);
        ex.Which.Message.Should().Contain("suspended");
    }

    [Fact]
    public async Task LoginAsync_returns_jwt_with_tenant_and_permission_claims()
    {
        var tenantId = Guid.NewGuid();
        var user = ActiveUser(tenantId, "Admin@Hospital.TEST");
        user.Email = "admin@hospital.test";
        var tenant = ActiveTenant(tenantId);

        _db.Setup(x => x.QueryFirstOrDefaultAsync<User>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _hasher.Setup(h => h.Verify("ok", "stored-hash")).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<Tenant>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var result = await CreateSut().LoginAsync(new LoginRequest("Admin@Hospital.TEST", "ok"));

        result.User.Email.Should().Be("admin@hospital.test");
        result.Tenant.Id.Should().Be(tenantId);
        result.User.Permissions.Should().Contain(CureFlowPermissions.PatientView);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Claims.First(c => c.Type == "tenant_id").Value.Should().Be(tenantId.ToString());
        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Where(c => c.Type == CureFlowPermissions.ClaimType)
            .Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { CureFlowPermissions.PatientView, CureFlowPermissions.UserCreate });

        _permissions.Verify(p => p.AssignLegacyRoleAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _db.Verify(d => d.UpdateAsync(user, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterTenantAsync_rejects_reserved_slug()
    {
        var act = () => CreateSut().RegisterTenantAsync(new RegisterTenantRequest(
            "Admin",
            "Owner",
            "owner@test.com",
            "TestHospital123!",
            null));

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*reserved slug*");
    }

    [Fact]
    public async Task RegisterTenantAsync_rejects_name_without_alphanumeric()
    {
        var act = () => CreateSut().RegisterTenantAsync(new RegisterTenantRequest(
            "!!!",
            "Owner",
            "owner@test.com",
            "TestHospital123!",
            null));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*at least one letter or number*");
    }

    [Fact]
    public async Task RegisterTenantAsync_rejects_duplicate_email()
    {
        _db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.Is<string>(s => s.Contains("Tenants") && s.Contains("Slug")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.Is<string>(s => s.Contains("Users") && s.Contains("Email")),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => CreateSut().RegisterTenantAsync(new RegisterTenantRequest(
            "Alpha Care Hospital",
            "Owner",
            "taken@test.com",
            "TestHospital123!",
            null));

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*already registered*");
        _db.Verify(d => d.TransactionAsync(It.IsAny<Func<ICureFlowDbSession, Task>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_forbids_unauthenticated_caller()
    {
        _tenant.Setup(t => t.IsAuthenticated).Returns(false);

        var act = () => CreateSut().CreateUserAsync(new CreateUserRequest(
            "Jane", "jane@test.com", "secret", "doctor"));

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task CreateUserAsync_rejects_invalid_role()
    {
        _tenant.Setup(t => t.IsAuthenticated).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var act = () => CreateSut().CreateUserAsync(new CreateUserRequest(
            "Jane", "jane@test.com", "secret", "not_a_role"));

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Invalid role*");
    }

    [Fact]
    public async Task CreateUserAsync_rejects_duplicate_email_in_tenant()
    {
        _tenant.Setup(t => t.IsAuthenticated).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var act = () => CreateSut().CreateUserAsync(new CreateUserRequest(
            "Jane", "jane@test.com", "secret", "doctor"));

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateUserAsync_hashes_password_and_returns_permissions()
    {
        _tenant.Setup(t => t.IsAuthenticated).Returns(true);
        _db.Setup(x => x.QueryFirstOrDefaultAsync<int?>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);
        User? inserted = null;
        _db.Setup(x => x.InsertAsync(It.IsAny<User>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<User, bool, CancellationToken>((u, _, _) => inserted = u)
            .Returns(Task.CompletedTask);

        var dto = await CreateSut().CreateUserAsync(new CreateUserRequest(
            "Jane", "Jane@Hospital.TEST", "secret", "doctor", Specialty: "Cardio"));

        dto.Email.Should().Be("jane@hospital.test");
        dto.Role.Should().Be(UserRole.Doctor);
        dto.Specialty.Should().Be("Cardio");
        dto.Permissions.Should().Contain(CureFlowPermissions.UserCreate);
        inserted.Should().NotBeNull();
        inserted!.PasswordHash.Should().Be("hashed-password");
        _hasher.Verify(h => h.Hash("secret"), Times.Once);
        _audit.Verify(a => a.LogAsync("user.create", "user", inserted.Id.ToString(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
/// Documented WebApplicationFactory / HTTP cases for a future Integration project
/// (<c>dotnet-backend-test/integration/</c>). Controllers only forward to services + rate limits.
/// </summary>
public class AuthHttpIntegrationCases
{
    /*
     * Suggested WebApplicationFactory cases (CureFlow.IntegrationTests):
     *
     * POST /api/auth/login
     *   - 200 + JWT when credentials valid; Authorization Bearer works on /api/auth/me
     *   - 401 for bad password / unknown email
     *   - 403 for rejected / suspended tenant
     *   - rate limit "login" returns 429 after threshold
     *
     * POST /api/auth/register-tenant
     *   - 200 creates PendingApproval tenant
     *   - 422 reserved slug / duplicate email
     *   - rate limit "register-tenant"
     *
     * POST /api/auth/register
     *   - 401 without JWT; 403 without Permission:User.Create
     *   - 200 with User.Create permission
     *
     * GET /api/auth/me and /api/auth/session
     *   - 401 anonymous; 200 with tenant lifecycle for route gates
     *
     * POST /api/platform/auth/bootstrap
     *   - 200 first owner; 409 when owner exists; validation 422 for short password
     *   - rate limit "platform-bootstrap"
     *
     * POST /api/platform/auth/login
     *   - 200 platform JWT with platform_user=true claim; 401 bad credentials
     *
     * GET /api/platform/auth/setup-status
     *   - NeedsSetup true/false without auth
     */

    [Fact(Skip = "Requires WebApplicationFactory + API host — place under CureFlow.IntegrationTests.")]
    public void Http_auth_login_register_and_platform_bootstrap_documented_for_integration()
    {
        // Intentionally empty: keeps the HTTP surface discoverable from the UnitTest project.
    }
}
