using System.Net;
using System.Net.Sockets;

namespace CureFlow.Application.Common;

/// <summary>Outcome of validating an untrusted, externally supplied URL.</summary>
public sealed record RemoteUrlValidation(bool IsAllowed, Uri? Uri, string? Reason)
{
    public static RemoteUrlValidation Allow(Uri uri) => new(true, uri, null);
    public static RemoteUrlValidation Deny(string reason) => new(false, null, reason);
}

/// <summary>
/// SSRF guards for server-side fetches of attacker-influenced URLs (e.g. webhook-supplied
/// media links). Pure logic with an injectable DNS resolver so it is fully unit-testable.
/// </summary>
public static class SafeRemoteUrl
{
    /// <summary>Parses <paramref name="url"/> and requires an absolute <c>https</c> URL.</summary>
    public static bool TryGetHttpsUri(string? url, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed))
            return false;
        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        uri = parsed;
        return true;
    }

    /// <summary>
    /// Validates that <paramref name="url"/> is an absolute https URL whose host resolves
    /// exclusively to public, routable IP addresses. Private/loopback/link-local/unique-local
    /// and cloud metadata addresses are rejected to defend against SSRF.
    /// </summary>
    /// <param name="resolveHost">
    /// Host resolver (normally <see cref="Dns.GetHostAddressesAsync(string, CancellationToken)"/>);
    /// injected so the guard can be unit-tested without real DNS.
    /// </param>
    public static async Task<RemoteUrlValidation> ValidatePublicHttpsAsync(
        string? url,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHost,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(resolveHost);

        if (!TryGetHttpsUri(url, out var uri) || uri is null)
            return RemoteUrlValidation.Deny("URL must be an absolute https URL.");

        // If the host is an IP literal, validate it directly without a DNS lookup.
        if (IPAddress.TryParse(uri.Host, out var literal))
        {
            return IsBlockedAddress(literal)
                ? RemoteUrlValidation.Deny($"Destination address {literal} is not a public address.")
                : RemoteUrlValidation.Allow(uri);
        }

        IPAddress[] addresses;
        try
        {
            addresses = await resolveHost(uri.DnsSafeHost, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return RemoteUrlValidation.Deny($"Could not resolve host '{uri.Host}': {ex.Message}");
        }

        if (addresses is null || addresses.Length == 0)
            return RemoteUrlValidation.Deny($"Host '{uri.Host}' did not resolve to any address.");

        foreach (var address in addresses)
        {
            if (IsBlockedAddress(address))
                return RemoteUrlValidation.Deny($"Host '{uri.Host}' resolves to a non-public address ({address}).");
        }

        return RemoteUrlValidation.Allow(uri);
    }

    /// <summary>
    /// Returns true when <paramref name="address"/> is private, loopback, link-local,
    /// unique-local, unspecified, or otherwise not safe to fetch from a server.
    /// </summary>
    public static bool IsBlockedAddress(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        // Treat IPv4-mapped IPv6 (::ffff:a.b.c.d) as its IPv4 form for range checks.
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))          // 127.0.0.0/8, ::1
            return true;
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) // 0.0.0.0, ::
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
            return IsBlockedIPv4(address.GetAddressBytes());

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal)            // fe80::/10
                return true;
            if (address.IsIPv6SiteLocal)            // fec0::/10 (deprecated)
                return true;
            if (IsUniqueLocalIPv6(address.GetAddressBytes())) // fc00::/7
                return true;
            return false;
        }

        // Unknown / unsupported address family — reject by default.
        return true;
    }

    private static bool IsBlockedIPv4(byte[] b)
    {
        // 0.0.0.0/8 (incl. unspecified)
        if (b[0] == 0) return true;
        // 10.0.0.0/8
        if (b[0] == 10) return true;
        // 127.0.0.0/8 (also handled by IsLoopback)
        if (b[0] == 127) return true;
        // 100.64.0.0/10 (RFC 6598 shared/CGNAT — not publicly routable)
        if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;
        // 169.254.0.0/16 (link-local, includes 169.254.169.254 metadata)
        if (b[0] == 169 && b[1] == 254) return true;
        // 172.16.0.0/12
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
        // 192.168.0.0/16
        if (b[0] == 192 && b[1] == 168) return true;
        return false;
    }

    // fc00::/7 — unique local addresses.
    private static bool IsUniqueLocalIPv6(byte[] b) => (b[0] & 0xFE) == 0xFC;
}

/// <summary>
/// Reads a stream into memory while enforcing a hard byte cap. Defends against unbounded
/// buffering when a response has no (or a misleading) Content-Length (e.g. chunked transfer).
/// </summary>
public static class BoundedStreamReader
{
    /// <summary>
    /// Reads the whole stream, aborting as soon as the running total exceeds
    /// <paramref name="maxBytes"/>. Returns the bytes read, or <c>null</c> when the cap is exceeded.
    /// </summary>
    public static async Task<byte[]?> ReadAllWithCapAsync(Stream stream, long maxBytes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (maxBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(chunk.AsMemory(0, chunk.Length), ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
                return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
