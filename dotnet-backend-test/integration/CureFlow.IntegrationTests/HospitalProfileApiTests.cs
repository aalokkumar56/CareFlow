using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/hospital-profile</c>.
/// Skips when no test database is available.
/// </summary>
public class HospitalProfileApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HospitalProfileApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_api_hospital_profile_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/hospital-profile");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_api_hospital_profile_departments_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/hospital-profile/departments");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_api_hospital_profile_with_auth_returns_profile()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Hospital Profile Get");
        var response = await auth.Client.GetAsync("/api/hospital-profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().NotBeNullOrWhiteSpace();
        body.TryGetProperty("departments", out var departments).Should().BeTrue();
        departments.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Get_api_hospital_profile_departments_with_auth_returns_ok_array()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Hospital Depts");
        var response = await auth.Client.GetAsync("/api/hospital-profile/departments");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Put_api_hospital_profile_updates_tagline_and_returns_ok()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Hospital Profile Put");
        var tagline = $"Care you can trust {Guid.NewGuid():N}"[..32];

        using var content = new StringContent(
            JsonSerializer.Serialize(new { tagline, timezone = "Asia/Kolkata" }, IntegrationTestSupport.JsonOptions),
            Encoding.UTF8,
            "application/json");

        var put = await auth.Client.PutAsync("/api/hospital-profile", content);
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var putBody = await put.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        putBody.GetProperty("ok").GetBoolean().Should().BeTrue();

        var get = await auth.Client.GetAsync("/api/hospital-profile");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = await get.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        profile.GetProperty("tagline").GetString().Should().Be(tagline);
        profile.GetProperty("timezone").GetString().Should().Be("Asia/Kolkata");
    }
}
