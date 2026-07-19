using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/notification-preferences</c>.
/// Skips when no test database is available.
/// </summary>
public class NotificationPreferencesApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationPreferencesApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Get_api_notification_preferences_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/notification-preferences");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Get_api_notification_preferences_with_auth_returns_ok_array()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notif Prefs Get");
        var response = await auth.Client.GetAsync("/api/notification-preferences");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.ValueKind.Should().Be(JsonValueKind.Array);
        body.GetArrayLength().Should().BeGreaterThan(0);

        var first = body[0];
        first.TryGetProperty("notification_type", out _).Should().BeTrue();
        first.TryGetProperty("in_app_enabled", out _).Should().BeTrue();
        first.TryGetProperty("can_configure", out _).Should().BeTrue();
    }

    [SkippableFact]
    public async Task Put_api_notification_preferences_then_reset_returns_no_content()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notif Prefs Put");

        var get = await auth.Client.GetAsync("/api/notification-preferences");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var prefs = await get.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        prefs.GetArrayLength().Should().BeGreaterThan(0);

        string? configurableType = null;
        foreach (var item in prefs.EnumerateArray())
        {
            if (item.TryGetProperty("can_configure", out var can) && can.GetBoolean()
                && item.TryGetProperty("notification_type", out var typeProp))
            {
                configurableType = typeProp.GetString();
                break;
            }
        }

        configurableType.Should().NotBeNullOrWhiteSpace();

        var update = await auth.Client.PutAsJsonAsync(
            "/api/notification-preferences",
            new
            {
                preferences = new[]
                {
                    new { notification_type = configurableType, in_app_enabled = false },
                },
            },
            IntegrationTestSupport.JsonOptions);
        update.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var reset = await auth.Client.PostAsync("/api/notification-preferences/reset", null);
        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SkippableFact]
    public async Task Get_api_notification_preferences_role_defaults_requires_auth()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var anonymous = _factory.CreateClient();
        var unauth = await anonymous.GetAsync("/api/notification-preferences/role-defaults");
        unauth.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Get_api_notification_preferences_role_defaults_with_owner_returns_ok()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notif Prefs Roles");
        var response = await auth.Client.GetAsync("/api/notification-preferences/role-defaults");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }
}
