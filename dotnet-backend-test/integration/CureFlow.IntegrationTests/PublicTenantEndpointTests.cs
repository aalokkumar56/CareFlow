using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// Anonymous public tenant branding endpoints (<c>/api/public/tenant*</c>).
/// Skips when no test database is available.
/// </summary>
public class PublicTenantEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PublicTenantEndpointTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task GetTenantBySlug_UnknownSlug_ReturnsNotFound()
    {
        await using var host = IntegrationTestSupport.CreateHostOrSkip(_factory);

        var client = host.CreateClient();
        var response = await client.GetAsync($"/api/public/tenant/missing-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [SkippableFact]
    public async Task GetTenantBySlug_KnownSlug_ReturnsPublicBrandingWithoutAuth()
    {
        await using var host = IntegrationTestSupport.CreateHostOrSkip(_factory);

        var client = host.CreateClient();
        var (slug, name) = await RegisterTenantAsync(client);

        var response = await client.GetAsync($"/api/public/tenant/{slug}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.GetProperty("slug").GetString().Should().Be(slug);
        body.GetProperty("name").GetString().Should().Be(name);
        body.GetProperty("lifecycle_status").GetString().Should().NotBeNullOrWhiteSpace();

        // Public payload must not expose internal identifiers.
        body.TryGetProperty("id", out _).Should().BeFalse();
        body.TryGetProperty("contact_email", out _).Should().BeFalse();
    }

    [SkippableFact]
    public async Task GetTenantContext_WithoutHeader_ReturnsUnresolved()
    {
        await using var host = IntegrationTestSupport.CreateHostOrSkip(_factory);

        var client = host.CreateClient();
        var response = await client.GetAsync("/api/public/tenant-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.GetProperty("resolved").GetBoolean().Should().BeFalse();
    }

    [SkippableFact]
    public async Task GetTenantContext_WithXTenantSlug_ReturnsResolvedTenant()
    {
        await using var host = IntegrationTestSupport.CreateHostOrSkip(_factory);

        var client = host.CreateClient();
        var (slug, name) = await RegisterTenantAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/public/tenant-context");
        request.Headers.TryAddWithoutValidation("X-Tenant-Slug", slug);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.GetProperty("resolved").GetBoolean().Should().BeTrue();
        body.GetProperty("slug").GetString().Should().Be(slug);
        body.GetProperty("name").GetString().Should().Be(name);
    }

    private static async Task<(string Slug, string Name)> RegisterTenantAsync(HttpClient client)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var name = $"Public Tenant Hospital {suffix}";
        var email = $"public-tenant-{suffix}@e2e.cureflow.test";

        var register = await client.PostAsJsonAsync(
            "/api/auth/register-tenant",
            new
            {
                hospital_name = name,
                admin_name = "Public Admin",
                admin_email = email,
                admin_password = IntegrationTestSupport.DefaultHospitalPassword,
                phone = "+919900000088",
            },
            IntegrationTestSupport.JsonOptions);

        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenant = await register.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        var slug = tenant.GetProperty("slug").GetString();
        slug.Should().NotBeNullOrWhiteSpace();
        return (slug!, name);
    }
}
