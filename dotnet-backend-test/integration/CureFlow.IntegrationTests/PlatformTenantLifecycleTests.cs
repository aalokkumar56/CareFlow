using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Infrastructure.Identity;
using CureFlow.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Platform tenant lifecycle: register → approve → suspend → activate.
/// Requires a reachable test database (CUREFLOW_TEST_CONNECTION or local appsettings).
/// </summary>
public class PlatformTenantLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    // Matches PlatformUserSeeder defaults used by local/E2E ops login.
    private const string PlatformEmail = PlatformUserSeeder.DefaultEmail;
    private const string PlatformPassword = PlatformUserSeeder.DefaultPassword;
    private const string PlatformName = PlatformUserSeeder.DefaultName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public PlatformTenantLifecycleTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RegisterApproveSuspendActivate_TransitionsLifecycleStatus()
    {
        await using var host = CreateHost();

        var client = host.CreateClient();
        var platformToken = await EnsurePlatformTokenAsync(client);
        platformToken.Should().NotBeNullOrWhiteSpace();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"lifecycle-{suffix}@e2e.cureflow.test";
        const string password = "TestHospital123!";

        var register = await client.PostAsync(
            "/api/auth/register-tenant",
            JsonContent(new
            {
                HospitalName = $"Lifecycle Hospital {suffix}",
                AdminName = "Lifecycle Admin",
                AdminEmail = email,
                AdminPassword = password,
                Phone = "+919900000099",
            }));
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenant = await register.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var tenantId = tenant.GetProperty("id").GetGuid();
        tenantId.Should().NotBe(Guid.Empty);
        tenant.GetProperty("lifecycle_status").GetString().Should().Be("pending_approval");

        using var platformClient = host.CreateClient();
        platformClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", platformToken);

        var approve = await platformClient.PatchAsync($"/api/platform/tenants/{tenantId}/approve", null);
        approve.StatusCode.Should().Be(HttpStatusCode.OK);
        var approveBody = await approve.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        approveBody.GetProperty("lifecycle_status").GetString().Should().Be("Active");

        var suspend = await platformClient.PatchAsync($"/api/platform/tenants/{tenantId}/suspend", null);
        suspend.StatusCode.Should().Be(HttpStatusCode.OK);
        var suspendBody = await suspend.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        suspendBody.GetProperty("lifecycle_status").GetString().Should().Be("Suspended");

        var activate = await platformClient.PatchAsync($"/api/platform/tenants/{tenantId}/activate", null);
        activate.StatusCode.Should().Be(HttpStatusCode.OK);
        var activateBody = await activate.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        activateBody.GetProperty("lifecycle_status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task PlatformTenants_List_RequiresPlatformAuth()
    {
        await using var host = CreateHost();

        var client = host.CreateClient();
        var anonymous = await client.GetAsync("/api/platform/tenants");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var token = await EnsurePlatformTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var authorized = await client.GetAsync("/api/platform/tenants");
        authorized.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task EnsurePlatformOpsCredentialsAsync()
    {
        var cs = IntegrationTestHelpers.RequireConnectionString();
        var hash = new BcryptPasswordHasher().Hash(PlatformPassword);

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using var upsert = new NpgsqlCommand(
            """
            INSERT INTO "PlatformUsers"
              ("Id", "Name", "Email", "PasswordHash", "IsActive", "IsDeleted", "CreatedAt", "UpdatedAt")
            VALUES
              (@id, @name, @email, @hash, true, false, NOW(), NOW())
            ON CONFLICT ("Email") DO UPDATE
              SET "PasswordHash" = EXCLUDED."PasswordHash",
                  "IsActive" = true,
                  "IsDeleted" = false,
                  "UpdatedAt" = NOW()
            """,
            conn);
        // PlatformUsers unique index is on Email; use lowercase to match seeder.
        upsert.Parameters.AddWithValue("id", Guid.NewGuid());
        upsert.Parameters.AddWithValue("name", PlatformName);
        upsert.Parameters.AddWithValue("email", PlatformEmail.ToLowerInvariant());
        upsert.Parameters.AddWithValue("hash", hash);
        await upsert.ExecuteNonQueryAsync();
    }

    private static async Task<string> EnsurePlatformTokenAsync(HttpClient client)
    {
        await EnsurePlatformOpsCredentialsAsync();

        var statusResponse = await client.GetAsync("/api/platform/auth/setup-status");
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var status = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var needsSetup = status.GetProperty("needs_setup").GetBoolean();

        HttpResponseMessage authResponse;
        if (needsSetup)
        {
            authResponse = await client.PostAsync(
                "/api/platform/auth/bootstrap",
                JsonContent(new
                {
                    Name = PlatformName,
                    Email = PlatformEmail,
                    Password = PlatformPassword,
                }));
        }
        else
        {
            authResponse = await client.PostAsync(
                "/api/platform/auth/login",
                JsonContent(new
                {
                    Email = PlatformEmail,
                    Password = PlatformPassword,
                }));
        }

        var bodyText = await authResponse.Content.ReadAsStringAsync();
        authResponse.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "platform auth must succeed with seeded ops credentials. Body: {0}",
            bodyText);

        var body = JsonSerializer.Deserialize<JsonElement>(bodyText, JsonOptions);
        var token = body.GetProperty("access_token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        return token!;
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    private WebApplicationFactory<Program> CreateHost() =>
        IntegrationTestHelpers.CreateHost(_factory);
}
