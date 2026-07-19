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
/// HTTP coverage for <c>/api/referrals</c> (create + analytics) and referring-doctor setup.
/// Skips when no test database / e2e seed login is available.
/// </summary>
public class ReferralsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;

    public ReferralsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Referrals_Endpoints_RequireAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();

        (await client.GetAsync("/api/referrals/analytics"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsJsonAsync(
                "/api/referrals",
                new { doctor_id = Guid.NewGuid(), patient_id = Guid.NewGuid(), revenue = 100m },
                JsonOpts))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Analytics_WithoutReferralView_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithPermissions(CureFlowPermissions.DashboardView);

        (await client.GetAsync("/api/referrals/analytics"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WithoutReferralManage_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithPermissions(CureFlowPermissions.ReferralView);

        (await client.PostAsJsonAsync(
                "/api/referrals",
                new { doctor_id = Guid.NewGuid(), patient_id = Guid.NewGuid(), revenue = 50m },
                JsonOpts))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateReferral_And_Analytics_ForHospitalAdmin()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        var token = await LoginE2eAdminAsync();
        token.Should().NotBeNullOrWhiteSpace("auth token required when database is available");

        using var client = CreateAuthedClient(token);

        var doctor = await client.PostAsJsonAsync(
            "/api/doctors",
            new
            {
                name = $"Ref Doc {Guid.NewGuid():N}"[..24],
                clinic = "Integration Clinic",
                specialty = "General",
                phone = "+919988776655",
                category = "specialist",
                reconnect_days = 30,
            },
            JsonOpts);
        doctor.StatusCode.Should().Be(HttpStatusCode.OK);
        var doctorId = (await doctor.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        var suffix = Guid.NewGuid().ToString("N")[..6];
        var patient = await client.PostAsJsonAsync(
            "/api/patients",
            new CreatePatientRequest(
                $"Referral Patient {suffix}", "+919955443322", 35, Gender.Female, BloodGroup.BPos,
                "General", "referral", doctorId, null, null, null, null, null, null, null, null),
            JsonOpts);
        patient.StatusCode.Should().Be(HttpStatusCode.OK);
        var patientId = (await patient.Content.ReadFromJsonAsync<JsonElement>(JsonOpts)).GetProperty("id").GetGuid();

        var create = await client.PostAsJsonAsync(
            "/api/referrals",
            new
            {
                doctor_id = doctorId,
                patient_id = patientId,
                revenue = 1500m,
                notes = "integration referral",
            },
            JsonOpts);
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        createBody.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);

        var analytics = await client.GetAsync("/api/referrals/analytics");
        analytics.StatusCode.Should().Be(HttpStatusCode.OK);
        var analyticsBody = await analytics.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        analyticsBody.GetProperty("total_referrals").GetInt32().Should().BeGreaterThan(0);
        analyticsBody.GetProperty("total_revenue").GetDecimal().Should().BeGreaterThan(0);
        analyticsBody.TryGetProperty("top_doctors", out _).Should().BeTrue();
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
