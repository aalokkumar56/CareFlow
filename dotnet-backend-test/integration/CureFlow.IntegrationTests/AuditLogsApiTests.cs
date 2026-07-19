using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
using CureFlow.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/audit-logs</c> (authz + tenant-scoped list).
/// Skips when no test database / e2e seed login is available.
/// </summary>
public class AuditLogsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;

    public AuditLogsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task List_RequiresAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();

        (await client.GetAsync("/api/audit-logs"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task List_WithoutAuditView_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithPermissions(CureFlowPermissions.DashboardView);

        (await client.GetAsync("/api/audit-logs"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task List_ReturnsOk_AndRespectsLimit_ForHospitalAdmin()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var token = await LoginE2eAdminAsync(MultiHospitalE2eSeeder.Hospitals[0].AdminEmail);
        token.Should().NotBeNullOrWhiteSpace("auth token required when database is available");

        using var client = CreateAuthedClient(token);

        // Ensure at least one audit row exists for this tenant.
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var createPatient = await client.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(
                $"Audit Patient {suffix}", "+919900112233", 42, Gender.Male, BloodGroup.OPos,
                "General", "walk_in", null, null, null, null, null, null, null, null, null),
            JsonOpts);
        createPatient.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await client.GetAsync("/api/audit-logs?limit=5");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().BeLessThanOrEqualTo(5);
        body.GetArrayLength().Should().BeGreaterThan(0);
        var actions = body.EnumerateArray().Select(row =>
            {
                if (!row.TryGetProperty("action", out var action))
                    return null;
                return action.GetString();
            }).ToList();
        actions.Should().Contain("patient.create");
    }

    [SkippableFact]
    public async Task List_DoesNotExposeOtherTenantPatientCreateAction()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var hospitalA = MultiHospitalE2eSeeder.Hospitals[0];
        var hospitalB = MultiHospitalE2eSeeder.Hospitals[1];

        var tokenA = await LoginE2eAdminAsync(hospitalA.AdminEmail);
        var tokenB = await LoginE2eAdminAsync(hospitalB.AdminEmail);
        tokenA.Should().NotBeNullOrWhiteSpace("e2e hospital A admin login required");
        tokenB.Should().NotBeNullOrWhiteSpace("e2e hospital B admin login required");

        var markerName = $"AuditIso {Guid.NewGuid():N}"[..28];
        using (var clientA = CreateAuthedClient(tokenA))
        {
            var create = await clientA.PostAsJsonAsync(
                "/api/patients",
                new CreatePatientRequest(
                    markerName, "+919911223344", 30, Gender.Other, BloodGroup.ABPos,
                    "General", "walk_in", null, null, null, null, null, null, null, null, null),
                JsonOpts);
            create.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        using var clientB = CreateAuthedClient(tokenB);
        var listB = await clientB.GetAsync("/api/audit-logs?limit=200");
        listB.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await listB.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.ValueKind.Should().Be(JsonValueKind.Array);

        // Tenant B must not see Tenant A's audit metadata / entity markers for the exclusive create.
        var serialized = body.GetRawText();
        serialized.Should().NotContain(markerName);
    }

    private HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        var client = _factory.CreateClient();
        var token = TestJwtHelper.CreateToken(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            permissions: permissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string?> LoginE2eAdminAsync(string email)
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = MultiHospitalE2eSeeder.DefaultPassword },
            JsonOpts);

        if (response.StatusCode != HttpStatusCode.OK)
            return null;

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var token = body.GetProperty("access_token").GetString();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private HttpClient CreateAuthedClient(string accessToken)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
