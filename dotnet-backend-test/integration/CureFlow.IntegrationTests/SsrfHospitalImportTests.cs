using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CureFlow.Application.DTOs;
using CureFlow.Domain.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// SSRF guards on <c>POST /api/hospital-profile/import</c> (user-supplied website URL).
/// Skips when no test database is available.
/// </summary>
public class SsrfHospitalImportTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly CustomWebApplicationFactory _factory;
    private WebApplicationFactory<Program>? _host;
    private string? _accessToken;

    public SsrfHospitalImportTests(CustomWebApplicationFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        // Do not Skip here (IAsyncLifetime); tests call RequireDatabase below.
        if (!_factory.HasDatabase)
            return;

        _host = IntegrationTestHelpers.CreateHostOrSkip(_factory);
        _accessToken = await RegisterActivateAndLoginAsync(_host);
    }

    public Task DisposeAsync()
    {
        _host?.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("https://127.0.0.1/")]
    [InlineData("https://169.254.169.254/latest/meta-data/")]
    [InlineData("https://192.168.1.1/")]
    [InlineData("https://10.0.0.1/")]
    [InlineData("file:///etc/passwd")]
    public async Task Import_WithSsrfUrl_IsRejected(string url)
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);
        _host.Should().NotBeNull();
        _accessToken.Should().NotBeNullOrWhiteSpace();

        var client = _host!.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        var response = await client.PostAsync(
            "/api/hospital-profile/import",
            new StringContent(
                JsonSerializer.Serialize(new { url }, JsonOptions),
                Encoding.UTF8,
                "application/json"));

        // ValidationException → 422; must not succeed or hang on internal targets.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("error").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Import_WithoutAuth_ReturnsUnauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);
        _host.Should().NotBeNull();

        var client = _host!.CreateClient();
        var response = await client.PostAsync(
            "/api/hospital-profile/import",
            new StringContent(
                JsonSerializer.Serialize(new { url = "https://127.0.0.1/" }, JsonOptions),
                Encoding.UTF8,
                "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> RegisterActivateAndLoginAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"ssrf-import-{suffix}@e2e.cureflow.test";
        var password = IntegrationTestSupport.DefaultHospitalPassword;

        var register = await client.PostAsJsonAsync(
            "/api/auth/register-tenant",
            new RegisterTenantRequest(
                $"Ssrf Import Hospital {suffix}",
                "Ssrf Admin",
                email,
                password,
                "+919900000077"),
            JsonOptions);
        register.StatusCode.Should().Be(HttpStatusCode.OK);

        var tenant = await register.Content.ReadFromJsonAsync<TenantDto>(JsonOptions);
        tenant.Should().NotBeNull();

        // Avoid depending on platform ops credentials — mark tenant Active for CRM APIs.
        await ActivateTenantAsync(tenant!.Id);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(email, password),
            JsonOptions);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.Should().NotBeNull();
        auth!.AccessToken.Should().NotBeNullOrWhiteSpace();
        return auth.AccessToken;
    }

    private static async Task ActivateTenantAsync(Guid tenantId)
    {
        var cs = IntegrationDb.ResolveConnectionString()
            ?? throw new InvalidOperationException("Test DB connection required.");

        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            """
            UPDATE "Tenants"
            SET "LifecycleStatus" = @status,
                "ApprovedAt" = @approvedAt,
                "UpdatedAt" = @approvedAt
            WHERE "Id" = @id
            """,
            conn);
        cmd.Parameters.AddWithValue("status", (int)TenantLifecycleStatus.Active);
        cmd.Parameters.AddWithValue("approvedAt", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("id", tenantId);
        var updated = await cmd.ExecuteNonQueryAsync();
        updated.Should().Be(1);
    }
}
