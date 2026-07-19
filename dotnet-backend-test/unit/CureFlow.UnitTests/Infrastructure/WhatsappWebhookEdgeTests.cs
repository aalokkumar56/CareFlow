using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CureFlow.Infrastructure.External;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Edge cases for webhook signature verification and phone_number_id routing
/// beyond the happy-path coverage in <see cref="WhatsappWebhookProcessorTests"/>.
/// </summary>
public class WhatsappWebhookSignatureEdgeTests
{
    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void VerifyMetaSignature_rejects_missing_sha256_prefix()
    {
        const string body = """{"event":"message_received"}""";
        const string secret = "secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hex = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

        WhatsappWebhookProcessor.VerifyMetaSignature(body, hex, secret).Should().BeFalse();
    }

    [Fact]
    public void VerifyMetaSignature_rejects_empty_header()
    {
        WhatsappWebhookProcessor.VerifyMetaSignature("{}", "", "secret").Should().BeFalse();
        WhatsappWebhookProcessor.VerifyMetaSignature("{}", null, "secret").Should().BeFalse();
    }

    [Fact]
    public void VerifyMetaSignature_is_case_insensitive_on_hex_digest()
    {
        const string body = """{"ok":true}""";
        const string secret = "case-secret";
        var lower = Sign(body, secret);
        var upper = "sha256=" + lower["sha256=".Length..].ToUpperInvariant();

        WhatsappWebhookProcessor.VerifyMetaSignature(body, upper, secret).Should().BeTrue();
    }

    [Fact]
    public void VerifyMetaSignature_rejects_tampered_body()
    {
        const string secret = "relay-secret";
        var sig = Sign("""{"event":"message_received","text":{"body":"Hi"}}""", secret);

        WhatsappWebhookProcessor.VerifyMetaSignature(
                """{"event":"message_received","text":{"body":"Bye"}}""", sig, secret)
            .Should().BeFalse();
    }

    [Fact]
    public void TryVerifyWebhookSignature_rejects_sha256_with_wrong_secret()
    {
        const string body = """{"event":"message_status"}""";
        var sig = Sign(body, "correct-secret");

        WhatsappWebhookProcessor.TryVerifyWebhookSignature(
                body, sig, "other-secret", NullLogger.Instance)
            .Should().BeFalse();
    }

    [Fact]
    public void TryVerifyWebhookSignature_accepts_empty_body_when_signature_matches()
    {
        const string body = "";
        const string secret = "empty-body-secret";
        var sig = Sign(body, secret);

        WhatsappWebhookProcessor.TryVerifyWebhookSignature(body, sig, secret, NullLogger.Instance)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("sha1=abcdef")]
    [InlineData("SHA256=abcdef")]
    public void VerifyMetaSignature_rejects_wrong_algorithm_prefix(string header)
    {
        WhatsappWebhookProcessor.VerifyMetaSignature("{}", header, "secret").Should().BeFalse();
    }

    [Fact]
    public void VerifyMetaSignature_rejects_wrong_length_digest()
    {
        WhatsappWebhookProcessor.VerifyMetaSignature("{}", "sha256=deadbeef", "secret")
            .Should().BeFalse();
    }
}

public class WhatsappWebhookRoutingEdgeTests
{
    [Fact]
    public void ExtractPhoneNumberId_reads_camelCase_root_field()
    {
        using var doc = JsonDocument.Parse("""{"phoneNumberId":"camel-tenant-line"}""");
        WhatsappWebhookProcessor.ExtractPhoneNumberId(doc.RootElement)
            .Should().Be("camel-tenant-line");
    }

    [Fact]
    public void ExtractPhoneNumberId_reads_root_metadata_when_entry_absent()
    {
        using var doc = JsonDocument.Parse(
            """{"metadata":{"phone_number_id":"root-meta-line"},"messages":[]}""");
        WhatsappWebhookProcessor.ExtractPhoneNumberId(doc.RootElement)
            .Should().Be("root-meta-line");
    }

    [Fact]
    public void ExtractPhoneNumberId_prefers_direct_field_over_nested_meta()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "phone_number_id": "direct-wins",
              "entry": [{
                "changes": [{
                  "value": { "metadata": { "phone_number_id": "nested-loses" } }
                }]
              }]
            }
            """);
        WhatsappWebhookProcessor.ExtractPhoneNumberId(doc.RootElement)
            .Should().Be("direct-wins");
    }

    [Fact]
    public void ExtractPhoneNumberId_skips_empty_entry_arrays()
    {
        using var doc = JsonDocument.Parse("""{"entry":[]}""");
        WhatsappWebhookProcessor.ExtractPhoneNumberId(doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void ExtractPhoneNumberId_skips_changes_without_metadata()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "entry": [{
                "changes": [{
                  "value": {
                    "messages": [{ "from": "919100000001", "text": { "body": "Hi" } }]
                  }
                }]
              }]
            }
            """);
        WhatsappWebhookProcessor.ExtractPhoneNumberId(doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void ExtractPhoneNumberId_ignores_blank_direct_value_and_falls_through()
    {
        using var doc = JsonDocument.Parse(
            """
            {
              "phone_number_id": "   ",
              "entry": [{
                "changes": [{
                  "value": { "metadata": { "phoneNumberId": "from-nested" } }
                }]
              }]
            }
            """);
        WhatsappWebhookProcessor.ExtractPhoneNumberId(doc.RootElement)
            .Should().Be("from-nested");
    }
}
