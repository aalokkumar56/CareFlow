using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Cross-tenant IDOR: a caller from tenant B must not read tenant A resources
/// (404 via tenant-scoped queries, or 403 when JWT tenant_id is spoofed).
/// Prefers MultiHospitalE2eSeeder logins; skips when seed/platform data is unavailable.
/// </summary>
public class CrossTenantIdorTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public CrossTenantIdorTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task GetPatient_OfOtherTenant_Returns404()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var pair = await ResolveTenantPairAsync();
        Skip.If(pair is null, "Multi-hospital e2e seed login unavailable.");

        using var clientA = CreateAuthedClient(pair!.Value.AuthA.AccessToken);
        using var clientB = CreateAuthedClient(pair.Value.AuthB.AccessToken);

        var patientId = await EnsurePatientAsync(clientA, $"Idor Victim {Guid.NewGuid():N}"[..28]);
        var getOwn = await clientA.GetAsync($"/api/patients/{patientId}");
        getOwn.StatusCode.Should().Be(HttpStatusCode.OK);
        var patientName = (await getOwn.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("name").GetString() ?? "";

        var foreignGet = await clientB.GetAsync($"/api/patients/{patientId}");
        foreignGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var body = await foreignGet.Content.ReadAsStringAsync();
        body.Should().NotContain(patientName);
        body.Should().NotContain(patientId.ToString());

        pair.Value.AuthA.TenantId.Should().NotBe(pair.Value.AuthB.TenantId);
    }

    [SkippableFact]
    public async Task ListPatients_DoesNotLeakOtherTenantPatient()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var pair = await ResolveTenantPairAsync();
        Skip.If(pair is null, "Multi-hospital e2e seed login unavailable.");

        using var clientA = CreateAuthedClient(pair!.Value.AuthA.AccessToken);
        using var clientB = CreateAuthedClient(pair.Value.AuthB.AccessToken);

        var exclusiveName = $"Idor Exclusive {Guid.NewGuid():N}"[..28];
        var patientId = await EnsurePatientAsync(clientA, exclusiveName);

        var listB = await clientB.GetAsync("/api/patients?page=1&page_size=100");
        listB.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listB.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var ids = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .ToList();
        var names = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString())
            .ToList();

        ids.Should().NotContain(patientId);
        names.Should().NotContain(exclusiveName);
    }

    [SkippableFact]
    public async Task JwtWithSpoofedTenantId_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var pair = await ResolveTenantPairAsync();
        Skip.If(pair is null, "Multi-hospital e2e seed login unavailable.");

        // Real user from B, but tenant_id claim forged to A — TenantMiddleware must reject.
        var spoofed = TestJwtHelper.CreateToken(
            userId: pair!.Value.AuthB.UserId,
            tenantId: pair.Value.AuthA.TenantId,
            permissions: CureFlowPermissions.All,
            email: pair.Value.AuthB.Email,
            role: RoleNames.Admin);

        using var spoofClient = _factory.CreateClient();
        spoofClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", spoofed);

        var response = await spoofClient.GetAsync("/api/patients");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Guid> EnsurePatientAsync(HttpClient client, string name)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var create = await client.PostAsync(
            "/api/patients",
            JsonContent(new
            {
                Name = name,
                Phone = $"+9198{suffix}",
                Department = "General",
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
            .GetProperty("id").GetGuid();
    }

    private async Task<(HospitalAuth AuthA, HospitalAuth AuthB)?> ResolveTenantPairAsync()
    {
        var hospitalA = MultiHospitalE2eSeeder.Hospitals[0];
        var hospitalB = MultiHospitalE2eSeeder.Hospitals[1];

        var authA = await LoginAsync(hospitalA.AdminEmail, MultiHospitalE2eSeeder.DefaultPassword);
        var authB = await LoginAsync(hospitalB.AdminEmail, MultiHospitalE2eSeeder.DefaultPassword);
        if (authA is not null && authB is not null)
            return (authA, authB);

        return null;
    }

    private async Task<HospitalAuth?> LoginAsync(string email, string password)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { email, password }));

        if (response.StatusCode != HttpStatusCode.OK)
            return null;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        if (!body.TryGetProperty("access_token", out var tokenProp))
            return null;

        var token = tokenProp.GetString();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var userId = body.GetProperty("user").GetProperty("id").GetGuid();
        var tenantId = body.GetProperty("tenant").GetProperty("id").GetGuid();
        return new HospitalAuth(tenantId, userId, email, token);
    }

    private HttpClient CreateAuthedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");

    private sealed record HospitalAuth(Guid TenantId, Guid UserId, string Email, string AccessToken);
}
