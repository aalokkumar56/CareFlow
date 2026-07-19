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
/// HTTP coverage for <c>/api/users</c> (INT-500…INT-509, INT-525).
/// Skips when no test database is available.
/// </summary>
public class UsersApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;

    public UsersApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Int500_ListUsers_RequiresAuth()
    {
        await using var host = CreateAuthHost();
        var client = host.CreateClient();
        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Int500_501_502_503_ListGetCreateUpdate_User()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "users-crud");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"staff-{suffix}@e2e.cureflow.test";
        const string password = "StaffUser123!";

        var create = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest($"Staff {suffix}", email, password, "staff", Phone: "+919911100001"),
            JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var userId = created.GetProperty("id").GetGuid();
        userId.Should().NotBe(Guid.Empty);
        created.GetProperty("email").GetString().Should().Be(email);

        var list = await client.GetAsync("/api/users");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        listBody.GetArrayLength().Should().BeGreaterThan(0);
        listBody.EnumerateArray().Any(u => u.GetProperty("id").GetGuid() == userId).Should().BeTrue();

        var get = await client.GetAsync($"/api/users/{userId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        getBody.GetProperty("id").GetGuid().Should().Be(userId);

        var update = await client.PatchAsJsonAsync(
            $"/api/users/{userId}",
            new UpdateUserRequest($"Staff Updated {suffix}", null, null, null, Phone: "+919911100002"),
            JsonOptions);
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        updated.GetProperty("name").GetString().Should().Contain("Updated");
    }

    [Fact]
    public async Task Int504_505_506_DisableEnableAndResetPassword()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "users-lifecycle");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"lifecycle-{suffix}@e2e.cureflow.test";
        const string password = "StaffUser123!";
        const string newPassword = "StaffUser456!";

        var create = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest($"Lifecycle {suffix}", email, password, "reception"),
            JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var disable = await client.PostAsync($"/api/users/{userId}/disable", null);
        disable.StatusCode.Should().Be(HttpStatusCode.OK);

        using (var anon = host.CreateClient())
        {
            var blocked = await anon.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), JsonOptions);
            blocked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var enable = await client.PostAsync($"/api/users/{userId}/enable", null);
        enable.StatusCode.Should().Be(HttpStatusCode.OK);

        var reset = await client.PostAsJsonAsync(
            $"/api/users/{userId}/reset-password",
            new ResetPasswordRequest(newPassword, GenerateTemporary: false),
            JsonOptions);
        reset.StatusCode.Should().Be(HttpStatusCode.OK);

        using var loginClient = host.CreateClient();
        var loginOld = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), JsonOptions);
        loginOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var loginNew = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, newPassword), JsonOptions);
        loginNew.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Int507_508_509_AssignRolesPermissionsAndDelete()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "users-assign");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"assign-{suffix}@e2e.cureflow.test";

        var create = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest($"Assign {suffix}", email, "StaffUser123!", "staff"),
            JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var assignRole = await client.PostAsJsonAsync(
            $"/api/users/{userId}/assign-roles",
            new AssignRolesRequest(UserRole.Nurse),
            JsonOptions);
        assignRole.StatusCode.Should().Be(HttpStatusCode.OK);
        var roleBody = await assignRole.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        roleBody.GetProperty("role").GetString().Should().BeOneOf("nurse", "Nurse");

        var assignPerms = await client.PostAsJsonAsync(
            $"/api/users/{userId}/assign-permissions",
            new AssignPermissionsRequest([CureFlowPermissions.PatientView, CureFlowPermissions.DashboardView]),
            JsonOptions);
        assignPerms.StatusCode.Should().Be(HttpStatusCode.OK);
        var permsBody = await assignPerms.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        permsBody.GetProperty("ok").GetBoolean().Should().BeTrue();

        var delete = await client.DeleteAsync($"/api/users/{userId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/users/{userId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        getBody.GetProperty("is_active").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Int525_ListUsers_FiltersByQueryAndActive()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        await RegisterApproveOnboardAndLoginAsync(host, client, "users-filter");

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueName = $"FilterMe{suffix}";
        var email = $"filter-{suffix}@e2e.cureflow.test";

        var create = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(uniqueName, email, "StaffUser123!", "doctor"),
            JsonOptions);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var userId = (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions)).GetProperty("id").GetGuid();

        var byQ = await client.GetAsync($"/api/users?q={uniqueName}");
        byQ.StatusCode.Should().Be(HttpStatusCode.OK);
        var byQBody = await byQ.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        byQBody.EnumerateArray().Any(u => u.GetProperty("id").GetGuid() == userId).Should().BeTrue();

        await client.PostAsync($"/api/users/{userId}/disable", null);

        var activeOnly = await client.GetAsync("/api/users?is_active=true");
        activeOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        var activeBody = await activeOnly.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        activeBody.EnumerateArray().Any(u => u.GetProperty("id").GetGuid() == userId).Should().BeFalse();

        var inactiveOnly = await client.GetAsync("/api/users?is_active=false");
        inactiveOnly.StatusCode.Should().Be(HttpStatusCode.OK);
        var inactiveBody = await inactiveOnly.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        inactiveBody.EnumerateArray().Any(u => u.GetProperty("id").GetGuid() == userId).Should().BeTrue();
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
            new RegisterTenantRequest($"Users Hospital {label} {suffix}", "Users Admin", email, password, "+919900000010"),
            JsonOptions);
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenant = await register.Content.ReadFromJsonAsync<TenantDto>(JsonOptions);
        tenant.Should().NotBeNull();

        // Avoid depending on platform ops credentials — activate tenant in DB.
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
