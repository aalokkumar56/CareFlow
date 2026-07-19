using System.Net;
using System.Text;
using CureFlow.Application.Common;
using FluentAssertions;
using Xunit;

namespace CureFlow.UnitTests.Application;

public class SafeRemoteUrlTests
{
    private static Func<string, CancellationToken, Task<IPAddress[]>> Resolver(params string[] ips)
        => (_, _) => Task.FromResult(ips.Select(IPAddress.Parse).ToArray());

    [Theory]
    [InlineData("https://cdn.example.com/file.jpg")]
    [InlineData("https://lookaside.fbcdn.net/abc")]
    public void TryGetHttpsUri_accepts_https(string url)
    {
        SafeRemoteUrl.TryGetHttpsUri(url, out var uri).Should().BeTrue();
        uri.Should().NotBeNull();
    }

    [Theory]
    [InlineData("http://cdn.example.com/file.jpg")]
    [InlineData("ftp://cdn.example.com/file.jpg")]
    [InlineData("file:///etc/passwd")]
    [InlineData("not-a-url")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetHttpsUri_rejects_non_https(string? url)
    {
        SafeRemoteUrl.TryGetHttpsUri(url, out var uri).Should().BeFalse();
        uri.Should().BeNull();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.5.5.5")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")] // cloud metadata endpoint
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]       // CGNAT shared space
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:127.0.0.1")] // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1")]  // IPv4-mapped private
    public void IsBlockedAddress_blocks_private_and_metadata(string ip)
    {
        SafeRemoteUrl.IsBlockedAddress(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("157.240.221.35")] // public Meta/CDN range
    [InlineData("2606:4700:4700::1111")]
    public void IsBlockedAddress_allows_public(string ip)
    {
        SafeRemoteUrl.IsBlockedAddress(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_allows_public_host()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://cdn.example.com/file.jpg", Resolver("93.184.216.34"));

        result.IsAllowed.Should().BeTrue();
        result.Uri.Should().NotBeNull();
        result.Uri!.Host.Should().Be("cdn.example.com");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_http_scheme()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "http://cdn.example.com/file.jpg", Resolver("93.184.216.34"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_host_resolving_to_private_ip()
    {
        // DNS-rebinding style: a public-looking host that resolves to an internal address.
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://internal.attacker.example/secret", Resolver("169.254.169.254"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("non-public");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_when_any_resolved_ip_is_private()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://mixed.example/file", Resolver("8.8.8.8", "10.0.0.5"));

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_rejects_ip_literal_loopback()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://127.0.0.1/file", Resolver(/* never called */ "8.8.8.8"));

        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_denies_when_resolution_fails()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://does-not-resolve.example/file",
            (_, _) => throw new InvalidOperationException("dns failure"));

        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("resolve");
    }

    [Fact]
    public async Task ValidatePublicHttpsAsync_denies_when_no_addresses()
    {
        var result = await SafeRemoteUrl.ValidatePublicHttpsAsync(
            "https://empty.example/file", Resolver());

        result.IsAllowed.Should().BeFalse();
    }
}

public class BoundedStreamReaderTests
{
    [Fact]
    public async Task Reads_full_payload_within_cap()
    {
        var data = Encoding.UTF8.GetBytes("hello world payload");
        using var source = new MemoryStream(data);

        var result = await BoundedStreamReader.ReadAllWithCapAsync(source, 1024);

        result.Should().NotBeNull();
        result.Should().Equal(data);
    }

    [Fact]
    public async Task Returns_null_when_exceeding_cap()
    {
        var data = new byte[2048];
        using var source = new MemoryStream(data);

        var result = await BoundedStreamReader.ReadAllWithCapAsync(source, 1024);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Allows_payload_exactly_at_cap()
    {
        var data = new byte[1024];
        using var source = new MemoryStream(data);

        var result = await BoundedStreamReader.ReadAllWithCapAsync(source, 1024);

        result.Should().NotBeNull();
        result!.Length.Should().Be(1024);
    }

    [Fact]
    public async Task Aborts_chunked_stream_without_length_once_over_cap()
    {
        // A stream that yields data in small chunks (no Content-Length analogue).
        using var source = new ChunkedStream(chunkSize: 100, totalBytes: 5000);

        var result = await BoundedStreamReader.ReadAllWithCapAsync(source, 1024);

        result.Should().BeNull();
    }

    private sealed class ChunkedStream : Stream
    {
        private readonly int _chunkSize;
        private long _remaining;

        public ChunkedStream(int chunkSize, long totalBytes)
        {
            _chunkSize = chunkSize;
            _remaining = totalBytes;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0) return 0;
            var n = (int)Math.Min(Math.Min(_chunkSize, count), _remaining);
            Array.Clear(buffer, offset, n);
            _remaining -= n;
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
