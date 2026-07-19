using System.Net;
using System.Net.Http.Headers;
using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Authenticated callers missing the required permission claim must receive 403
/// (ASP.NET authorization runs before tenant middleware).
/// </summary>
public class AuthorizationForbiddenTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationForbiddenTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetPatients_WithoutPatientView_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithoutPermission(CureFlowPermissions.DashboardView);

        var response = await client.GetAsync("/api/patients");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostPatients_WithoutPatientCreate_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithoutPermission(CureFlowPermissions.PatientView);

        var response = await client.PostAsync(
            "/api/patients",
            new StringContent("""{"name":"No Create","phone":"919999999999"}""", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCampaigns_WithoutCampaignView_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithoutPermission(CureFlowPermissions.PatientView);

        var response = await client.GetAsync("/api/campaigns");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUsers_WithoutUserView_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithoutPermission(CureFlowPermissions.DashboardView);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostAuthRegister_WithoutUserCreate_Returns403()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = CreateClientWithoutPermission(CureFlowPermissions.UserView);

        var response = await client.PostAsync(
            "/api/auth/register",
            new StringContent(
                """{"name":"Staff","email":"no-create@cureflow.test","password":"TestHospital123!","role":"Staff"}""",
                System.Text.Encoding.UTF8,
                "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient CreateClientWithoutPermission(params string[] grantedPermissions)
    {
        var client = _factory.CreateClient();
        var token = TestJwtHelper.CreateToken(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            permissions: grantedPermissions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
