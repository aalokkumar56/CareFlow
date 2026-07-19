using System.Net;
using System.Net.Http.Headers;
using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// JWT bearer rejection cases against a protected API route (tampered / expired / wrong audience).
/// Skips when no test database is available.
/// </summary>
public class JwtSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string ProtectedPath = "/api/hospital-profile";

    private readonly CustomWebApplicationFactory _factory;

    public JwtSecurityTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task ProtectedEndpoint_WithTamperedToken_ReturnsUnauthorized()
    {
        await using var host = IntegrationTestHelpers.CreateHostOrSkip(_factory, TestJwtHelper.JwtConfigOverrides());

        var token = TestJwtHelper.CreateToken(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            permissions: [CureFlowPermissions.SettingsView]);
        var tampered = TestJwtHelper.TamperSignature(token);

        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync(ProtectedPath);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task ProtectedEndpoint_WithExpiredToken_ReturnsUnauthorized()
    {
        await using var host = IntegrationTestHelpers.CreateHostOrSkip(_factory, TestJwtHelper.JwtConfigOverrides());

        // ClockSkew is 2 minutes — expire well beyond that.
        var token = TestJwtHelper.CreateToken(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            permissions: [CureFlowPermissions.SettingsView],
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1));

        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(ProtectedPath);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task ProtectedEndpoint_WithWrongAudience_ReturnsUnauthorized()
    {
        await using var host = IntegrationTestHelpers.CreateHostOrSkip(_factory, TestJwtHelper.JwtConfigOverrides());

        var token = TestJwtHelper.CreateToken(
            userId: Guid.NewGuid(),
            tenantId: Guid.NewGuid(),
            permissions: [CureFlowPermissions.SettingsView],
            audience: "not-cureflow-api");

        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(ProtectedPath);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
