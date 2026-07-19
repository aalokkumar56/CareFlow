using System.IdentityModel.Tokens.Jwt;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Dapper;
using CureFlow.Infrastructure.Persistence.Seeders;
using CureFlow.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using Xunit;

namespace CureFlow.UnitTests;

public class TenantSlugHelperTests
{
    [Theory]
    [InlineData("Alpha Care Hospital", "alpha-care-hospital")]
    [InlineData("  Beta   Health  ", "beta-health")]
    [InlineData("Hospital #1 (Main)", "hospital-1-main")]
    public void Slugify_NormalizesNames(string input, string expected) =>
        TenantSlugHelper.Slugify(input).Should().Be(expected);

    [Theory]
    [InlineData("admin")]
    [InlineData("API")]
    [InlineData("platform")]
    public void IsReserved_BlocksPlatformSlugs(string slug) =>
        TenantSlugHelper.IsReserved(slug).Should().BeTrue();

    [Fact]
    public void WithSuffix_KeepsWithinMaxLength()
    {
        var longBase = new string('a', 70);
        var result = TenantSlugHelper.WithSuffix(longBase, 12);
        result.Length.Should().BeLessOrEqualTo(TenantSlugHelper.MaxSlugLength);
        result.Should().EndWith("-12");
    }
}

[Collection("DatabaseIntegration")]
public class MultiTenantAuthTests
{
    [Fact]
    public async Task RegisterAndLogin_ReturnsMatchingTenantId_InJwtAndResponse()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var systemTenant = new CurrentTenant();
        var systemSession = new CureFlowDbSession(dataSource, systemTenant);
        await RbacSeeder.SeedAsync(systemSession);

        var auth = CreateAuthService(dataSource, systemTenant);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"mt-auth-{suffix}@e2e.cureflow.test";

        var tenant = await auth.RegisterTenantAsync(new RegisterTenantRequest(
            $"Multi Tenant Test {suffix}",
            "Test Admin",
            email,
            "TestHospital123!",
            "+919900000099"));

        tenant.Id.Should().NotBe(Guid.Empty);
        tenant.Slug.Should().Contain("multi-tenant-test");

        var login = await auth.LoginAsync(new LoginRequest(email, "TestHospital123!"));
        login.Tenant.Id.Should().Be(tenant.Id);
        login.User.Email.Should().Be(email);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        jwt.Claims.First(c => c.Type == "tenant_id").Value.Should().Be(tenant.Id.ToString());
    }

    [Fact]
    public async Task RegisterThreeHospitals_ProducesDistinctSlugsAndTenantIds()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var systemTenant = new CurrentTenant();
        var systemSession = new CureFlowDbSession(dataSource, systemTenant);
        await RbacSeeder.SeedAsync(systemSession);

        var auth = CreateAuthService(dataSource, systemTenant);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenants = new List<TenantDto>();

        for (var i = 1; i <= 3; i++)
        {
            var email = $"mt-{suffix}-h{i}@e2e.cureflow.test";
            var dto = await auth.RegisterTenantAsync(new RegisterTenantRequest(
                $"Hospital {i} {suffix}",
                $"Admin {i}",
                email,
                "TestHospital123!",
                $"+91990000010{i}"));
            tenants.Add(dto);
        }

        tenants.Select(t => t.Id).Should().OnlyHaveUniqueItems();
        tenants.Select(t => t.Slug).Should().OnlyHaveUniqueItems();

        foreach (var t in tenants)
        {
            var placeholders = await systemSession.QueryAsync<TemplatePlaceholder>(
                """
                SELECT * FROM "TemplatePlaceholders"
                WHERE "TenantId" = @tenantId AND "IsSystem" = true AND "IsDeleted" = false
                """,
                new { tenantId = t.Id },
                ignoreTenant: true);
            placeholders.Should().NotBeEmpty("register-tenant should seed default template placeholders");
        }
    }

    [Fact]
    public async Task RegisterTenant_RejectsReservedSlug()
    {
        var connectionString = TestDbConnection.Resolve();
        if (connectionString is null)
            return;

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var systemTenant = new CurrentTenant();
        var auth = CreateAuthService(dataSource, systemTenant);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var act = () => auth.RegisterTenantAsync(new RegisterTenantRequest(
            "Admin",
            "Bad Name",
            $"reserved-{suffix}@e2e.cureflow.test",
            "TestHospital123!",
            null));

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*reserved slug*");
    }

    private static AuthService CreateAuthService(NpgsqlDataSource dataSource, CurrentTenant tenantContext)
    {
        var session = new CureFlowDbSession(dataSource, tenantContext);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "integration-test-secret-key-min-32-chars",
                ["Jwt:Issuer"] = "cureflow-test",
                ["Jwt:Audience"] = "cureflow-api-test",
            })
            .Build();

        var audit = new Mock<IAuditService>();
        return new AuthService(
            session,
            new BcryptPasswordHasher(),
            new JwtTokenService(config),
            tenantContext,
            audit.Object,
            new UserPermissionService(session),
            NullLogger<AuthService>.Instance);
    }
}
