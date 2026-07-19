using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/admin/roles</c> and permissions catalog (INT-510…INT-514, INT-528).
/// Skips when no test database is available.
/// </summary>
public class RolesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;

    public RolesApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Int510_ListRoles_RequiresAuth()
    {
        await using var host = CreateAuthHost();
        var client = host.CreateClient();
        var response = await client.GetAsync("/api/admin/roles");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Int510_ListRoles_IncludesSeededRoleNames()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "roles-list");

        var list = await client.GetAsync("/api/admin/roles");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var names = body.EnumerateArray()
            .Select(r => r.GetProperty("name").GetString())
            .Where(n => n is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        names.Should().Contain(RoleNames.Admin);
        names.Should().Contain(RoleNames.Doctor);
        names.Should().Contain(RoleNames.Receptionist);
    }

    [Fact]
    public async Task Int511_512_513_CreateUpdateAndDeleteCustomRole()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "roles-crud");

        var roleName = $"Custom_Role_{Guid.NewGuid():N}"[..24];

        var create = await client.PostAsJsonAsync(
            "/api/admin/roles",
            new CreateRoleRequest(roleName, "integration custom role", [CureFlowPermissions.PatientView]),
            JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        created.GetProperty("name").GetString().Should().Be(roleName);

        var update = await client.PutAsJsonAsync(
            $"/api/admin/roles/{roleName}",
            new UpdateRoleRequest("updated description", [CureFlowPermissions.PatientView, CureFlowPermissions.DashboardView]),
            JsonOptions);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("description").GetString().Should().Be("updated description");
        updated.GetProperty("permissions").GetArrayLength().Should().Be(2);

        var delete = await client.DeleteAsync($"/api/admin/roles/{roleName}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetAsync("/api/admin/roles");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        listBody.EnumerateArray()
            .Any(r => string.Equals(r.GetProperty("name").GetString(), roleName, StringComparison.OrdinalIgnoreCase))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Int528_DeleteSystemRole_IsRejected()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "roles-system");

        var delete = await client.DeleteAsync($"/api/admin/roles/{RoleNames.Admin}");
        delete.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Int514_ListPermissionsCatalog_ContainsCureFlowPermissions()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "roles-perms");

        var response = await client.GetAsync("/api/admin/permissions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var codes = body.EnumerateArray()
            .Select(p =>
            {
                if (p.TryGetProperty("key", out var key))
                    return key.GetString();
                return p.GetProperty("code").GetString();
            })
            .Where(c => c is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        codes.Should().Contain(CureFlowPermissions.UserView);
        codes.Should().Contain(CureFlowPermissions.StaffView);
        codes.Should().Contain(CureFlowPermissions.PatientView);
        codes.Count.Should().BeGreaterThanOrEqualTo(CureFlowPermissions.All.Count);
    }

    private static async Task RegisterApproveOnboardAndLoginAsync(
        WebApplicationFactory<Program> host,
        HttpClient client,
        string label)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"{label}-{suffix}@e2e.cureflow.test";
        const string password = "TestHospital123!";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register-tenant",
            new RegisterTenantRequest($"Roles Hospital {label} {suffix}", "Roles Admin", email, password, "+919900000020"),
            JsonOptions);
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenant = await register.Content.ReadFromJsonAsync<TenantDto>(JsonOptions);
        tenant.Should().NotBeNull();

        await ActivateTenantAsync(tenant!.Id);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), JsonOptions);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var complete = await client.PostAsync("/api/onboarding/complete", null);
        complete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task ActivateTenantAsync(Guid tenantId)
    {
        var cs = IntegrationDb.ResolveConnectionString()
            ?? throw new InvalidOperationException("Test DB connection required.");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "OnboardingComplete" = true,
                "ApprovedAt" = @approvedAt,
                "UpdatedAt" = @approvedAt
            WHERE "Id" = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("status", (int)TenantLifecycleStatus.Active);
        cmd.Parameters.AddWithValue("approvedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", tenantId);
        var updated = await cmd.ExecuteNonQueryAsync();
        updated.Should().Be(1);
    }

    private WebApplicationFactory<Program> CreateAuthHost() =>
        CreateConfiguredHost(IntegrationDb.ResolveConnectionString());

    private WebApplicationFactory<Program> CreateHostOrSkip()
    {
        var connectionString = IntegrationTestHelpers.RequireConnectionString();
        return CreateConfiguredHost(connectionString);
    }

    private WebApplicationFactory<Program> CreateConfiguredHost(string? connectionString)
    {
        return _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
            builder.UseSetting("Jwt:Secret", CustomWebApplicationFactory.TestJwtSecret);
            builder.UseSetting("Jwt:Issuer", TestJwtHelper.Issuer);
            builder.UseSetting("Jwt:Audience", TestJwtHelper.Audience);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Testing",
                    ["Jwt:Secret"] = CustomWebApplicationFactory.TestJwtSecret,
                    ["Jwt:Issuer"] = TestJwtHelper.Issuer,
                    ["Jwt:Audience"] = TestJwtHelper.Audience,
                    ["Database:AutoMigrate"] = "false",
                    ["Database:Seed"] = "false",
                    ["Database:SeedPlatformUser"] = "false",
                };
                if (connectionString is not null)
                    settings["ConnectionStrings:Default"] = connectionString;
                config.AddInMemoryCollection(settings);
            });
        });
    }
}
