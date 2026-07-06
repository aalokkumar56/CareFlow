using System.Security.Cryptography;
using System.Text;
using CureFlow.Infrastructure.External;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CureFlow.Tests;

public class WhatsappWebhookProcessorTests
{
    [Fact]
    public void TryVerifyWebhookSignature_accepts_when_no_app_secret()
    {
        WhatsappWebhookProcessor.TryVerifyWebhookSignature("{}", null, null, NullLogger.Instance)
            .Should().BeTrue();
    }

    [Fact]
    public void TryVerifyWebhookSignature_accepts_when_secret_set_but_header_missing()
    {
        WhatsappWebhookProcessor.TryVerifyWebhookSignature("{}", null, "my-secret", NullLogger.Instance)
            .Should().BeTrue();
    }

    [Fact]
    public void TryVerifyWebhookSignature_rejects_invalid_signature()
    {
        WhatsappWebhookProcessor.TryVerifyWebhookSignature(
                "{}", "sha256=deadbeef", "my-secret", NullLogger.Instance)
            .Should().BeFalse();
    }

    [Fact]
    public void TryVerifyWebhookSignature_accepts_valid_meta_signature()
    {
        const string body = """{"event":"message_received","from":"+917600174070","text":{"body":"Hi"}}""";
        const string secret = "test-app-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var sig = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        WhatsappWebhookProcessor.TryVerifyWebhookSignature(body, sig, secret, NullLogger.Instance)
            .Should().BeTrue();
    }

    [Fact]
    public void VerifyMetaSignature_matches_whatsbiz_relay_format()
    {
        const string body =
            """{"event":"message_received","phone_number_id":"123","from":"+917600174070","message_id":"wamid.abc","type":"text","text":{"body":"Hello!"}}""";
        const string secret = "whatsbiz-webhook-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var sig = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        WhatsappWebhookProcessor.VerifyMetaSignature(body, sig, secret).Should().BeTrue();
    }
}
