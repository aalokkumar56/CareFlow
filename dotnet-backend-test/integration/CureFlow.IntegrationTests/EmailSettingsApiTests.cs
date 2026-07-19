using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP coverage for <c>/api/settings/email</c>.
/// Skips when no test database is available.
/// </summary>
public class EmailSettingsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EmailSettingsApiTests(CustomWebApplicationFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Get_api_settings_email_status_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/settings/email/status");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Get_api_settings_email_without_auth_returns_unauthorized()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/settings/email");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Get_api_settings_email_status_with_auth_returns_status_shape()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Email Status");
        var response = await auth.Client.GetAsync("/api/settings/email/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        body.GetProperty("is_configured").GetBoolean().Should().BeFalse();
        body.GetProperty("message").GetString()
            .Should().Be("Email is not configured. Add SMTP settings under Settings → Integrations.");
    }

    [SkippableFact]
    public async Task Get_api_settings_email_with_auth_returns_defaults_or_settings()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Email Get");
        var response = await auth.Client.GetAsync("/api/settings/email");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        body.GetProperty("smtp_port").GetInt32().Should().Be(587);
        body.GetProperty("use_ssl").GetBoolean().Should().BeTrue();
        body.GetProperty("send_with_whatsapp").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task Post_api_settings_email_saves_and_masks_password()
    {
        IntegrationTestHelpers.RequireDatabase(_factory.HasDatabase);

        using var auth = await IntegrationTestSupport.RegisterAndLoginHospitalAsync(_factory, "Email Save");
        var hostName = $"smtp-{Guid.NewGuid():N}.example.test";

        var save = await auth.Client.PostAsJsonAsync(
            "/api/settings/email",
            new
            {
                SmtpHost = hostName,
                SmtpPort = 587,
                SmtpUsername = "noreply@example.test",
                SmtpPassword = "secret-smtp-password",
                UseSsl = true,
                FromEmail = "noreply@example.test",
                FromName = "CureFlow Test",
                Enabled = false,
                SendWithWhatsApp = true,
            },
            IntegrationTestSupport.JsonOptions);
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        var saveBody = await save.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        saveBody.GetProperty("ok").GetBoolean().Should().BeTrue();

        var get = await auth.Client.GetAsync("/api/settings/email");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var settings = await get.Content.ReadFromJsonAsync<JsonElement>(IntegrationTestSupport.JsonOptions);
        settings.GetProperty("smtp_host").GetString().Should().Be(hostName);
        settings.GetProperty("smtp_username").GetString().Should().Be("noreply@example.test");
        settings.GetProperty("from_email").GetString().Should().Be("noreply@example.test");
        settings.GetProperty("has_smtp_password").GetBoolean().Should().BeTrue();
        settings.GetProperty("smtp_password").GetString().Should().Be("********");
        settings.GetProperty("smtp_port").GetInt32().Should().Be(587);
        settings.GetProperty("enabled").GetBoolean().Should().BeFalse();
        settings.GetProperty("use_ssl").GetBoolean().Should().BeTrue();
        // API GET uses explicit snake key; request binding uses SnakeCaseLower → send_with_whats_app.
        settings.GetProperty("send_with_whatsapp").GetBoolean().Should().BeTrue();
    }
}
