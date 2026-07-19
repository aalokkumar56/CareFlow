using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/campaigns</c> (create, list, preview, schedule, delete).
/// Skips when no PostgreSQL connection is available.
/// </summary>
public class CampaignsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HospitalPassword = "TestHospital123!";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly CustomWebApplicationFactory _factory;

    public CampaignsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Campaigns_Endpoints_RequireAuth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();

        (await client.GetAsync("/api/campaigns"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.GetAsync("/api/campaigns/suggested-drafts"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync(
                "/api/campaigns",
                JsonContent(new
                {
                    Name = "x",
                    MessageBody = "hi",
                    Audience = new { Tags = Array.Empty<string>() },
                })))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_List_Preview_Schedule_And_Delete_Campaign()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tag = $"camp-{suffix}";
        var campaignName = $"Integration Campaign {suffix}";
        var messageBody = "Hello from CureFlow integration test";
        await ProvisionActiveHospitalAsync(client, $"Campaigns Hospital {suffix}", $"campaigns-{suffix}@e2e.cureflow.test");
        await CreatePatientAsync(client, $"Campaign Patient {suffix}", DigitsPhone(suffix, "9185"), tag);

        var audience = new
        {
            Tags = new[] { tag },
            Departments = Array.Empty<string>(),
            Statuses = Array.Empty<string>(),
        };

        var preview = await client.PostAsync("/api/campaigns/preview-audience", JsonContent(audience));
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        (await preview.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("count").GetInt32().Should().BeGreaterThan(0);

        var create = await client.PostAsync(
            "/api/campaigns",
            JsonContent(new
            {
                Name = campaignName,
                Description = "integration draft",
                MessageBody = messageBody,
                Audience = audience,
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var campaignId = createBody.GetProperty("id").GetGuid();
        campaignId.Should().NotBe(Guid.Empty);
        createBody.GetProperty("status").GetString().Should().Be("draft");

        var list = await client.GetAsync("/api/campaigns");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        listBody.ValueKind.Should().Be(JsonValueKind.Array);
        var listed = listBody.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == campaignId);
        listed.GetProperty("name").GetString().Should().Be(campaignName);
        listed.GetProperty("message_body").GetString().Should().Be(messageBody);

        var get = await client.GetAsync($"/api/campaigns/{campaignId}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var campaign = getBody.GetProperty("campaign");
        campaign.GetProperty("id").GetGuid().Should().Be(campaignId);
        campaign.GetProperty("name").GetString().Should().Be(campaignName);
        campaign.GetProperty("message_body").GetString().Should().Be(messageBody);
        getBody.GetProperty("recipients").ValueKind.Should().Be(JsonValueKind.Array);

        var drafts = await client.GetAsync("/api/campaigns/suggested-drafts");
        drafts.StatusCode.Should().Be(HttpStatusCode.OK);
        (await drafts.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .ValueKind.Should().Be(JsonValueKind.Array);

        var patch = await client.PatchAsync(
            $"/api/campaigns/{campaignId}",
            JsonContent(new { Name = "Updated campaign name", MessageBody = "Updated body" }));
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterPatch = await client.GetAsync($"/api/campaigns/{campaignId}");
        getAfterPatch.StatusCode.Should().Be(HttpStatusCode.OK);
        var patched = (await getAfterPatch.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("campaign");
        patched.GetProperty("name").GetString().Should().Be("Updated campaign name");
        patched.GetProperty("message_body").GetString().Should().Be("Updated body");

        var schedule = await client.PostAsync(
            $"/api/campaigns/{campaignId}/schedule",
            JsonContent(new { ScheduledAt = DateTime.UtcNow.AddHours(2) }));
        schedule.StatusCode.Should().Be(HttpStatusCode.OK);
        (await schedule.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("status").GetString().Should().Be("scheduled");

        var delete = await client.DeleteAsync($"/api/campaigns/{campaignId}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PreviewAudience_IsScopedToCurrentTenantPatients()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var uniqueTag = $"iso-{suffix}";
        await ProvisionActiveHospitalAsync(client, $"Campaigns Iso {suffix}", $"campaigns-iso-{suffix}@e2e.cureflow.test");
        await CreatePatientAsync(client, $"Iso Patient {suffix}", DigitsPhone(suffix, "9184"), uniqueTag);

        var matching = await client.PostAsync(
            "/api/campaigns/preview-audience",
            JsonContent(new
            {
                Tags = new[] { uniqueTag },
                Departments = Array.Empty<string>(),
                Statuses = Array.Empty<string>(),
            }));
        matching.StatusCode.Should().Be(HttpStatusCode.OK);
        (await matching.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("count").GetInt32().Should().Be(1);

        var missing = await client.PostAsync(
            "/api/campaigns/preview-audience",
            JsonContent(new
            {
                Tags = new[] { "tag-that-does-not-exist-xyz" },
                Departments = Array.Empty<string>(),
                Statuses = Array.Empty<string>(),
            }));
        missing.StatusCode.Should().Be(HttpStatusCode.OK);
        (await missing.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("count").GetInt32().Should().Be(0);
    }

    private static async Task CreatePatientAsync(HttpClient client, string name, string phone, string tag)
    {
        var create = await client.PostAsync(
            "/api/patients",
            JsonContent(new
            {
                Name = name,
                Phone = phone,
                Age = 28,
                Gender = "other",
                Department = "Marketing",
                Tags = new[] { tag },
            }));
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        (await create.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
    }

    private static async Task ProvisionActiveHospitalAsync(HttpClient client, string hospitalName, string adminEmail)
    {
        var register = await client.PostAsync(
            "/api/auth/register-tenant",
            JsonContent(new
            {
                HospitalName = hospitalName,
                AdminName = "Campaigns Admin",
                AdminEmail = adminEmail,
                AdminPassword = HospitalPassword,
                Phone = "+919900000033",
            }));
        register.StatusCode.Should().Be(HttpStatusCode.OK);
        var tenantId = (await register.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        await ActivateTenantInDbAsync(tenantId);

        var login = await client.PostAsync(
            "/api/auth/login",
            JsonContent(new { Email = adminEmail, Password = HospitalPassword }));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var accessToken = (await login.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("access_token").GetString();
        accessToken.Should().NotBeNullOrWhiteSpace();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
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

    private static string DigitsPhone(string suffix, string prefix) =>
        prefix + $"{Math.Abs(suffix.GetHashCode(StringComparison.Ordinal)):D6}"[..6];

    private static StringContent JsonContent(object payload) =>
        new(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
}
