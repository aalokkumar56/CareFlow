using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Hospital auth login against the real API host (<see cref="CustomWebApplicationFactory"/>).
/// Skips when no test database is available (CUREFLOW_TEST_CONNECTION or local appsettings).
/// </summary>
public class AuthLoginTests : IClassFixture<CustomWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public AuthLoginTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndMatchingEmail()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"auth-login-{suffix}@e2e.cureflow.test";
        const string password = "TestHospital123!";

        var register = await client.PostAsync(
            "/api/auth/register-tenant",
            JsonContent(new
            {
                HospitalName = $"Auth Login Hospital {suffix}",
                AdminName = "Auth Admin",
                AdminEmail = email,
                AdminPassword = password,
                Phone = "+919900000099",
            }));
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { Email = email, Password = password }));
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("user").GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("tenant").GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        await using var host = CreateHostOrSkip();

        var client = host.CreateClient();
        var response = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new
            {
                Email = $"missing-{Guid.NewGuid():N}@e2e.cureflow.test",
                Password = "not-a-real-password",
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

    private WebApplicationFactory<Program> CreateHostOrSkip() =>
        IntegrationTestHelpers.CreateHostOrSkip(_factory);
}
