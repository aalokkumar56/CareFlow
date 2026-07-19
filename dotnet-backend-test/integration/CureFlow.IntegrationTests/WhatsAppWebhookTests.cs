using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CureFlow.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CureFlow.IntegrationTests;

/// <summary>
/// HTTP contract tests for <c>/api/whatsapp/webhook</c> (challenge + signature header paths).
/// </summary>
public class WhatsAppWebhookTests : IClassFixture<WhatsAppWebhookTests.Factory>
{
    private const string VerifyToken = "integration-verify-token";
    private const string AppSecret = "integration-app-secret";

    private readonly Factory _factory;
    private HttpClient? _client;

    public WhatsAppWebhookTests(Factory factory)
    {
        _factory = factory;
        _factory.Whatsapp.Reset();
        SetupDefaultWhatsappMock();
    }

    private HttpClient Client =>
        _client ??= _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    [Fact]
    public async Task Get_webhook_challenge_returns_challenge_when_token_valid()
    {
        const string challenge = "meta-challenge-12345";
        _factory.Whatsapp
            .Setup(w => w.VerifyWebhookSubscription(
                "subscribe",
                VerifyToken,
                out It.Ref<string?>.IsAny,
                challenge))
            .Returns((string _, string _, out string? outChallenge, string? challengeParam) =>
            {
                outChallenge = challengeParam;
                return true;
            });

        var response = await Client.GetAsync(
            $"/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token={VerifyToken}&hub.challenge={challenge}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be(challenge);
    }

    [Fact]
    public async Task Get_webhook_challenge_returns_forbid_when_token_invalid()
    {
        _factory.Whatsapp
            .Setup(w => w.VerifyWebhookSubscription(
                "subscribe",
                "wrong-token",
                out It.Ref<string?>.IsAny,
                "challenge-x"))
            .Returns((string _, string _, out string? outChallenge, string? _) =>
            {
                outChallenge = null;
                return false;
            });

        var response = await Client.GetAsync(
            "/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge=challenge-x");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_webhook_with_invalid_signature_returns_unauthorized_and_never_invokes_processor()
    {
        const string payload = """{"object":"whatsapp_business_account","entry":[]}""";
        const string badSignature = "sha256=deadbeef00000000000000000000000000000000000000000000000000000000";

        WhatsappWebhookSignature.IsValidMetaSignature(payload, badSignature, AppSecret).Should().BeFalse();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", badSignature);

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("ok").GetBoolean().Should().BeFalse();
        json.GetProperty("error").GetString().Should().Be("invalid_signature");

        _factory.Whatsapp.Verify(
            w => w.ProcessIncomingWebhookAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_webhook_with_missing_signature_returns_unauthorized_and_never_invokes_processor()
    {
        const string payload = """{"object":"whatsapp_business_account","entry":[]}""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        _factory.Whatsapp.Verify(
            w => w.ProcessIncomingWebhookAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Post_webhook_with_valid_signature_invokes_processor_and_returns_ok()
    {
        const string payload = """{"object":"whatsapp_business_account","entry":[{"id":"1","changes":[]}]}""";
        var validSignature = WhatsappWebhookSignature.ComputeMetaSignature(payload, AppSecret);

        WhatsappWebhookSignature.IsValidMetaSignature(payload, validSignature, AppSecret).Should().BeTrue();

        string? receivedBody = null;
        string? receivedSignature = null;
        _factory.Whatsapp
            .Setup(w => w.ProcessIncomingWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string?, CancellationToken>((body, sig, _) =>
            {
                receivedBody = body;
                receivedSignature = sig;
            })
            .Returns(Task.CompletedTask);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/whatsapp/webhook")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", validSignature);

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("ok").GetBoolean().Should().BeTrue();

        receivedBody.Should().Be(payload);
        receivedSignature.Should().Be(validSignature);
        WhatsappWebhookSignature.IsValidMetaSignature(receivedBody!, receivedSignature, AppSecret).Should().BeTrue();

        _factory.Whatsapp.Verify(
            w => w.ProcessIncomingWebhookAsync(payload, validSignature, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetupDefaultWhatsappMock()
    {
        _factory.Whatsapp
            .Setup(w => w.VerifyWebhookSubscription(
                It.IsAny<string>(),
                It.IsAny<string>(),
                out It.Ref<string?>.IsAny,
                It.IsAny<string?>()))
            .Returns((string mode, string token, out string? challenge, string? challengeParam) =>
            {
                challenge = challengeParam;
                return mode == "subscribe" && token == VerifyToken;
            });

        _factory.Whatsapp
            .Setup(w => w.ProcessIncomingWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _factory.Whatsapp
            .Setup(w => w.SendTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, "msg-1", (string?)null));
    }

    public sealed class Factory : CustomWebApplicationFactory
    {
        public Mock<IWhatsappService> Whatsapp { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // Mocked webhook contract tests do not need RBAC / live DB at startup.
                    ["Database:SeedRbac"] = "false",
                    ["WhatsApp:AppSecret"] = AppSecret,
                    ["WhatsApp:VerifyToken"] = VerifyToken,
                });
            });
            builder.ConfigureTestServices(services => ReplaceService(services, Whatsapp.Object));
        }
    }
}

/// <summary>Meta Cloud <c>X-Hub-Signature-256</c> helpers (same algorithm as production webhook verification).</summary>
internal static class WhatsappWebhookSignature
{
    public static string ComputeMetaSignature(string body, string appSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool IsValidMetaSignature(string body, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith("sha256=", StringComparison.Ordinal))
            return false;

        var expected = ComputeMetaSignature(body, appSecret);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader.ToLowerInvariant()));
    }
}
