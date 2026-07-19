using System.Net;
using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests;

/// <summary>
/// Additional SSRF edges for webhook-supplied media URLs and any server-side
/// fetch of untrusted links (including email-driven remote resources).
/// </summary>
public class SafeRemoteUrlSsrfEdgeTests
{
    private static Func<string, CancellationToken, Task<IPAddress[]>> Resolver(params string[] ips)
        => (_, _) => Task.FromResult(ips.Select(IPAddress.Parse).ToArray());

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData("gopher://cdn.example.com/1")]
    [InlineData("https://")]
    [InlineData("https:///path-only")]
    public void TryGetHttpsUri_rejects_exotic_or_incomplete_schemes(string? url)
    {
        SafeRemoteUrl.TryGetHttpsUri(url, out var uri).Should().BeFalse();
        uri.Should().BeNull();
    }

    [Fact]
    public void TryGetHttpsUri_trims_surrounding_whitespace()
    {
        SafeRemoteUrl.TryGetHttpsUri("  https://cdn.example.com/a.jpg  ", out var uri)
            .Should().BeTrue();
        uri!.Host.Should().Be("cdn.example.com");
    }

    [Fact]
    public void TryGetHttpsUri_accepts_https_with_userinfo()
    {
        // Parsing succeeds; ValidatePublicHttpsAsync still resolves the host.
        SafeRemoteUrl.TryGetHttpsUri("https://user:pass@cdn.example.com/file", out var uri)
            .Should().BeTrue();
        uri!.Host.Should().Be("cdn.example.com");
        uri.UserInfo.Should().Contain("user");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_allows_public_ipv4_literal()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://93.184.216.34/file.jpg",
            Resolver(/* must not be required for literals */ "10.0.0.1"));

        result.IsAllowed.Should().BeTrue();
        result.Uri!.Host.Should().Be("93.184.216.34");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_link_local_ipv4_literal()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://169.254.169.254/latest/meta-data/",
            Resolver("8.8.8.8"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("not a public");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_private_ipv6_literal()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://[fc00::1]/secret",
            Resolver("8.8.8.8"));

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_cgnat_literal()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://100.64.0.10/internal",
            Resolver("8.8.8.8"));

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_dns_rebinding_to_loopback_ipv6()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://evil.example/media",
            Resolver("::1"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("non-public");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_mapped_private_from_dns()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://mapped.example/media",
            Resolver("::ffff:192.168.0.5"));

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_propagates_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://cdn.example.com/file",
            (_, ct) => Task.FromCanceled<IPAddress[]>(ct),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_requires_resolver()
    {
        var act = () => SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://cdn.example.com/file",
            resolveHost: null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("https://127.0.0.1:8443/admin")]
    [InlineData("https://10.255.255.255/")]
    [InlineData("https://192.168.100.1/cgi-bin")]
    [InlineData("https://172.20.0.15/v1")]
    public async Task ValidatePublicHttpsAsync_blocks_common_internal_targets(string url)
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(url, Resolver("8.8.8.8"));
        result.IsAllowed.Should().BeFalse();
    }

    /// <summary>
    /// Email/webhook bodies sometimes embed absolute URLs with odd casing or default ports.
    /// </summary>
    [Theory]
    [InlineData("HTTPS://CDN.Example.COM/media/a.jpg")]
    [InlineData("https://cdn.example.com:443/media/a.jpg")]
    public async Task ValidatePublicHttpsAsync_allows_canonical_public_https_variants(string url)
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(url, Resolver("93.184.216.34"));
        result.IsAllowed.Should().BeTrue();
        result.Uri.Should().NotBeNull();
    }

    [Fact]
    public void IsBlockedAddress_rejects_null()
    {
        var act = () => SafeRemoteUrl.IsBlockedAddress(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}

public class BoundedStreamReaderEdgeTests
{
    [Fact]
    public async Task Rejects_negative_cap()
    {
        using var source = new MemoryStream(new byte[8]);
        var act = () => BoundedStreamReader.ReadAllWithCapAsync(source, -1);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Rejects_null_stream()
    {
        var act = () => BoundedStreamReader.ReadAllWithCapAsync(null!, 10);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Allows_empty_stream_within_cap()
    {
        using var source = new MemoryStream();
        var result = await BoundedStreamReader.ReadAllWithCapAsync(source, 1024);
        result.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Cap_of_zero_rejects_any_bytes()
    {
        using var source = new MemoryStream(new byte[] { 1 });
        var result = await BoundedStreamReader.ReadAllWithCapAsync(source, 0);
        result.Should().BeNull();
    }
}
