using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CureFlow.Application.Common;
using CureFlow.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/dashboard</c> (overview, clinical-overview, missed-revenue).
/// Skips when no test database / e2e seed login is available.
/// </summary>
public class DashboardApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public DashboardApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_Endpoints_RequireAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();

        (await client.GetAsync("/api/dashboard/overview"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/dashboard/clinical-overview"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/dashboard/missed-revenue"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Overview_And_MissedRevenue_WithoutDashboardView_Return403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithPermissions(CureFlowPermissions.PatientView);

        (await client.GetAsync("/api/dashboard/overview"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/dashboard/missed-revenue"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ClinicalOverview_WithoutAppointmentOrClinicalView_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        // Dashboard.View alone is not enough — clinical-overview also requires Appointment.View + Clinical.View.
        using var client = CreateClientWithPermissions(CureFlowPermissions.DashboardView);

        (await client.GetAsync("/api/dashboard/clinical-overview"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Overview_ReturnsExpectedShape_ForHospitalAdmin()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var token = await LoginE2eAdminAsync();
        token.Should().NotBeNullOrWhiteSpace("e2e hospital admin login must succeed when DB is available");

        using var client = CreateAuthedClient(token!);
        var response = await client.GetAsync("/api/dashboard/overview");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        body.GetProperty("total_patients").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("appointments_today").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("revenue_mtd").GetDecimal().Should().BeGreaterThanOrEqualTo(0m);
        body.GetProperty("upcoming_appointments").ValueKind.Should().Be(JsonValueKind.Array);
        body.GetProperty("new_inquiries_today").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        body.GetProperty("conversion_rate").GetDouble().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ClinicalOverview_And_MissedRevenue_ReturnOk_ForHospitalAdmin()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var token = await LoginE2eAdminAsync();
        token.Should().NotBeNullOrWhiteSpace("e2e hospital admin login must succeed when DB is available");

        using var client = CreateAuthedClient(token!);

        var clinical = await client.GetAsync("/api/dashboard/clinical-overview");
        clinical.StatusCode.Should().Be(HttpStatusCode.OK);
        var clinicalBody = await clinical.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var stats = clinicalBody.GetProperty("stats");
        stats.GetProperty("appointments_today").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        stats.GetProperty("completed_today").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        stats.GetProperty("waiting_check_in").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        clinicalBody.GetProperty("appointments_today").ValueKind.Should().Be(JsonValueKind.Array);
        clinicalBody.GetProperty("date").GetString().Should().NotBeNullOrWhiteSpace();
        clinicalBody.GetProperty("scope").GetString().Should().Be("mine");

        var missed = await client.GetAsync("/api/dashboard/missed-revenue");
        missed.StatusCode.Should().Be(HttpStatusCode.OK);
        var missedBody = await missed.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        missedBody.GetProperty("estimated_loss").GetDecimal().Should().BeGreaterThanOrEqualTo(0m);
        missedBody.GetProperty("recoverable_revenue").GetDecimal().Should().BeGreaterThanOrEqualTo(0m);
        missedBody.GetProperty("unanswered_inquiries").ValueKind.Should().Be(JsonValueKind.Array);
        missedBody.GetProperty("missed_appointments").ValueKind.Should().Be(JsonValueKind.Array);
        missedBody.GetProperty("category_totals").ValueKind.Should().Be(JsonValueKind.Object);
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

    private async Task<string?> LoginE2eAdminAsync()
    {
        var hospital = MultiHospitalE2eSeeder.Hospitals[0];
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = hospital.AdminEmail, password = MultiHospitalE2eSeeder.DefaultPassword },
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
