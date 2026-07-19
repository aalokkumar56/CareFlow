using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Hospital register / activate / login helpers for notifications and settings API suites.
/// </summary>
internal static class IntegrationTestSupport
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    internal const string DefaultHospitalPassword = "TestHospital123!";

    internal static WebApplicationFactory<Program> CreateHostOrSkip(
        CustomWebApplicationFactory factory,
        IDictionary<string, string?>? extraConfig = null) =>
        IntegrationTestHelpers.CreateHost(factory, extraConfig);

    /// <summary>
    /// Registers a hospital, marks it Active + onboarded in DB, then returns an authenticated client.
    /// </summary>
    internal static async Task<HospitalAuthContext> RegisterAndLoginHospitalAsync(
        CustomWebApplicationFactory factory,
        string namePrefix = "Api Test Hospital")
    {
        using var bootstrap = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = namePrefix.ToLowerInvariant().Replace(' ', '-');
        var email = $"{slug}-{suffix}@e2e.cureflow.test";
        var password = DefaultHospitalPassword;

        var register = await bootstrap.PostAsJsonAsync(
            "/api/auth/register-tenant",
            new
            {
                hospital_name = $"{namePrefix} {suffix}",
                admin_name = "Api Test Admin",
                admin_email = email,
                admin_password = password,
                phone = "+919900000099",
            },
            JsonOptions);
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenant = await register.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var tenantId = tenant.GetProperty("id").GetGuid();
        await ActivateTenantInDbAsync(tenantId);

        var login = await bootstrap.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password },
            JsonOptions);
        login.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = body.GetProperty("access_token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        var authClient = factory.CreateClient();
        authClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new HospitalAuthContext(authClient, email, password, token!);
    }

    private static async Task ActivateTenantInDbAsync(Guid tenantId)
    {
        var cs = IntegrationDb.ResolveConnectionString();
        cs.Should().NotBeNullOrWhiteSpace();

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = 1,
                "OnboardingComplete" = true,
                "ApprovedAt" = NOW(),
                "UpdatedAt" = NOW()
            WHERE "Id" = @id AND "IsDeleted" = false
            """,
            conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        var updated = await cmd.ExecuteNonQueryAsync();
        updated.Should().Be(1);
    }

    internal sealed class HospitalAuthContext : IDisposable
    {
        public HospitalAuthContext(HttpClient client, string email, string password, string accessToken)
        {
            Client = client;
            Email = email;
            Password = password;
            AccessToken = accessToken;
        }

        public HttpClient Client { get; }
        public string Email { get; }
        public string Password { get; }
        public string AccessToken { get; }

        public void Dispose() => Client.Dispose();
    }
}
