using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Platform tenant lifecycle: register → approve → suspend → activate.
/// Skips when no test database is available (CUREFLOW_TEST_CONNECTION or local appsettings).
/// </summary>
public class PlatformTenantLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    // Matches PlatformUserSeeder defaults used by local/E2E ops login.
    private const string PlatformEmail = "ops@cureflow.in";
    private const string PlatformPassword = "OpsAdmin123!";
    private const string PlatformName = "CureFlow Ops";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public PlatformTenantLifecycleTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task RegisterApproveSuspendActivate_TransitionsLifecycleStatus()
    {
        await using var host = CreateHostOrSkip();

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

    [SkippableFact]
    public async Task PlatformTenants_List_RequiresPlatformAuth()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        var anonymous = await client.GetAsync("/api/platform/tenants");
        anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var token = await EnsurePlatformTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var authorized = await client.GetAsync("/api/platform/tenants");
        authorized.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<string> EnsurePlatformTokenAsync(HttpClient client)
    {
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

        if (authResponse.StatusCode != HttpStatusCode.OK)
        {
            Skip.If(
                true,
                "Platform ops credentials unavailable in test DB (needs matching seed/bootstrap user).");
        }

        var body = await authResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = body.GetProperty("access_token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        return token!;
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    private WebApplicationFactory<Program> CreateHostOrSkip() =>
        IntegrationTestHelpers.CreateHostOrSkip(_factory);
}
