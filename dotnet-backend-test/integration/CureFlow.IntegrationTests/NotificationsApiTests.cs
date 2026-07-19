using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/notifications</c>.
/// Skips when no test database is available.
/// </summary>
public class NotificationsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public NotificationsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_api_notifications_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_api_notifications_unread_count_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/notifications/unread-count");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_api_notifications_with_auth_returns_ok_array()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notifications List");
        var response = await auth.Client.GetAsync("/api/notifications?unread_only=false&limit=20");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Get_api_notifications_unread_count_with_auth_returns_count()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notifications Count");
        var response = await auth.Client.GetAsync("/api/notifications/unread-count");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.GetProperty("count").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task Post_api_notifications_mark_all_read_returns_no_content()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notifications MarkAll");
        var response = await auth.Client.PostAsync("/api/notifications/mark-all-read", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Patch_and_delete_unknown_notification_returns_not_found()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Notifications Missing");
        var missingId = Guid.NewGuid();

        var markRead = await auth.Client.PatchAsync($"/api/notifications/{missingId}/read", null);
        markRead.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var delete = await auth.Client.DeleteAsync($"/api/notifications/{missingId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
