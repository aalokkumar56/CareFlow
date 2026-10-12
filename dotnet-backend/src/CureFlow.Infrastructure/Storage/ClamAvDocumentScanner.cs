using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using CureFlow.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CureFlow.Infrastructure.Storage;

// Local private socket only: clamd TCP has no native authentication/encryption.
// VERSION approval is deployment evidence, not a substitute for signature upkeep.
public sealed class ClamAvDocumentScanner : IDocumentScanner
{
    private readonly string socketPath;
    private readonly string approvedVersionHash;
    private readonly TimeSpan timeout;
    private const long MaximumBytes = 10 * 1024 * 1024;

    public ClamAvDocumentScanner(IConfiguration configuration)
    {
        var policy = configuration["DocumentScanning:ApprovedPolicyVersion"];
        socketPath = configuration["DocumentScanning:ClamAv:SocketPath"] ?? string.Empty;
        approvedVersionHash = configuration["DocumentScanning:ClamAv:ApprovedDaemonVersionSha256"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(policy) || policy.Length > 100 || policy.Any(char.IsControl)
            || !Path.IsPathFullyQualified(socketPath) || socketPath.Length > 100 || socketPath.Any(char.IsControl)
            || approvedVersionHash.Length != 64 || approvedVersionHash.Any(c => !char.IsAsciiHexDigit(c)))
            throw new InvalidOperationException("Local document scanner requires an approved policy, private absolute socket and approved daemon version hash.");
        var seconds = configuration.GetValue("DocumentScanning:ClamAv:TimeoutSeconds", 18);
        if (seconds is < 1 or > 20) throw new InvalidOperationException("Document scanner timeout must be between 1 and 20 seconds.");
        approvedVersionHash = approvedVersionHash.ToUpperInvariant();
        timeout = TimeSpan.FromSeconds(seconds);
    }

    public static IDocumentScanner Create(IConfiguration configuration) => configuration["DocumentScanning:Provider"] switch
    {
        null or "" or "Unavailable" => new UnavailableDocumentScanner(),
        "ClamAvLocal" => new ClamAvDocumentScanner(configuration),
        _ => throw new InvalidOperationException("Unsupported document scanner provider.")
    };

    public async Task<DocumentScanResult> ScanAsync(Stream content, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(timeout);
        try
        {
            var version = await VersionHashAsync(budget.Token);
            if (version != approvedVersionHash) return Failed("ScannerVersionUnapproved");
            using var socket = await ConnectAsync(budget.Token);
            await using var network = new NetworkStream(socket, ownsSocket: false);
            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), budget.Token);
            var buffer = new byte[64 * 1024];
            var size = new byte[4];
            long total = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, budget.Token)) != 0)
            {
                total += read;
                if (total > MaximumBytes) return Failed("ScannerContentTooLarge");
                BinaryPrimitives.WriteUInt32BigEndian(size, (uint)read);
                await network.WriteAsync(size, budget.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), budget.Token);
            }
            if (total == 0) return Failed("ScannerContentEmpty");
            Array.Clear(size);
            await network.WriteAsync(size, budget.Token);
            var reply = await ReadReplyAsync(network, budget.Token);
            // Bind acceptance to stable, explicitly approved engine/definitions evidence.
            if (await VersionHashAsync(budget.Token) != version) return Failed("ScannerVersionChanged");
            return InterpretReply(reply, version);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is SocketException or IOException or OperationCanceledException)
        {
            // Never expose daemon responses, detection labels, file content or paths.
            return Failed("ScannerUnavailable");
        }
    }

    public static DocumentScanResult InterpretReply(string reply, string versionHash)
    {
        if (reply == "stream: OK") return new("clean", "ClamAV", versionHash, "ScannerClean");
        if (reply.StartsWith("stream: ", StringComparison.Ordinal) && reply.EndsWith(" FOUND", StringComparison.Ordinal)
            && reply.Length > "stream:  FOUND".Length && !reply.Any(char.IsControl))
            return new("rejected", "ClamAV", versionHash, "MalwareDetected");
        return Failed("ScannerResponseInvalid");
    }

    private static DocumentScanResult Failed(string code) => new("failed", "ClamAV", "unavailable", code);

    private async Task<Socket> ConnectAsync(CancellationToken ct)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try { await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct); return socket; }
        catch { socket.Dispose(); throw; }
    }

    private async Task<string> VersionHashAsync(CancellationToken ct)
    {
        using var socket = await ConnectAsync(ct);
        await using var network = new NetworkStream(socket, ownsSocket: false);
        await network.WriteAsync("zVERSION\0"u8.ToArray(), ct);
        var version = await ReadReplyAsync(network, ct);
        if (!version.StartsWith("ClamAV ", StringComparison.Ordinal) || version.Count(c => c == '/') < 2)
            throw new IOException("Scanner version evidence is unavailable.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(version)));
    }

    private static async Task<string> ReadReplyAsync(Stream network, CancellationToken ct)
    {
        var buffer = new byte[512];
        using var reply = new MemoryStream();
        int read;
        while ((read = await network.ReadAsync(buffer, ct)) != 0)
        {
            if (reply.Length + read > 4096) throw new IOException("Scanner response exceeded its bound.");
            reply.Write(buffer, 0, read);
        }
        var bytes = reply.ToArray();
        if (bytes.Length < 2 || bytes[^1] != 0 || bytes.AsSpan(0, bytes.Length - 1).Contains((byte)0)
            || bytes.AsSpan(0, bytes.Length - 1).ContainsAnyExceptInRange((byte)32, (byte)126))
            throw new IOException("Scanner response framing is invalid.");
        return Encoding.ASCII.GetString(bytes, 0, bytes.Length - 1);
    }
}
