using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Cross-tenant IDOR: a caller from tenant B must not read tenant A resources
/// (404 via tenant-scoped queries, or 403 when JWT tenant_id is spoofed).
/// Registers two hospitals per run so seed data is not required.
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

    [Fact]
    public async Task GetPatient_OfOtherTenant_Returns404()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var pair = await ResolveTenantPairAsync();

        using var clientA = CreateAuthedClient(pair.AuthA.AccessToken);
        using var clientB = CreateAuthedClient(pair.AuthB.AccessToken);

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

        pair.AuthA.TenantId.Should().NotBe(pair.AuthB.TenantId);
    }

    [Fact]
    public async Task ListPatients_DoesNotLeakOtherTenantPatient()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var pair = await ResolveTenantPairAsync();

        using var clientA = CreateAuthedClient(pair.AuthA.AccessToken);
        using var clientB = CreateAuthedClient(pair.AuthB.AccessToken);

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

    [Fact]
    public async Task JwtWithSpoofedTenantId_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var pair = await ResolveTenantPairAsync();

        // Real user from B, but tenant_id claim forged to A — TenantMiddleware must reject.
        var spoofed = TestJwtHelper.CreateToken(
            userId: pair.AuthB.UserId,
            tenantId: pair.AuthA.TenantId,
            permissions: CureFlowPermissions.All,
            email: pair.AuthB.Email,
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

    private async Task<(HospitalAuth AuthA, HospitalAuth AuthB)> ResolveTenantPairAsync()
    {
        using var authA = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Idor Hospital A");
        using var authB = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Idor Hospital B");

        var userA = await LoginAsync(authA.Email, authA.Password);
        var userB = await LoginAsync(authB.Email, authB.Password);
        userA.Should().NotBeNull("hospital A must login after register/activate");
        userB.Should().NotBeNull("hospital B must login after register/activate");
        return (userA!, userB!);
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
