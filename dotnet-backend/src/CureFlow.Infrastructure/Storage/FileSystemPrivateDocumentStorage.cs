using System.Security.Cryptography;
using CureFlow.Application.Common;
using CureFlow.Application.Interfaces;
using CureFlow.Application.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CureFlow.Infrastructure.Storage;

// Shared durable filesystem volume; no public URL, no destructive delete operation.
public sealed class FileSystemPrivateDocumentStorage(IOptions<StorageOptions> options, IHostEnvironment environment) : IPrivateDocumentStorage
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private string Root()
    {
        var configured = options.Value.PrivateRoot;
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new InvalidOperationException("Storage:PrivateRoot must be an absolute private durable directory.");
        var root = Path.GetFullPath(configured);
        var application = Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar);
        if (root.Equals(application, PathComparison) || root.StartsWith(application + Path.DirectorySeparatorChar, PathComparison))
            throw new InvalidOperationException("Private document storage must be outside the application directory.");
        RejectLinks(root);
        Directory.CreateDirectory(root);
        RejectLinks(root);
        return root;
    }

    public async Task<PrivateDocumentObject> StageAsync(Guid tenantId, Stream source, string contentType, long declaredLength, long maximumLength, CancellationToken ct)
    {
        if (tenantId == Guid.Empty) throw new DomainException("Tenant identity is required.", 403);
        if (declaredLength <= 0 || declaredLength > maximumLength) throw new ValidationException("Invalid document length.");
        var version = Guid.NewGuid();
        var key = $"{tenantId:N}/{version:N}";
        var path = ObjectPath(tenantId, key, staging: true);
        RejectLinks(Path.GetDirectoryName(path)!);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RejectLinks(Path.GetDirectoryName(path)!);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long length = 0;
        await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
        {
            var buffer = new byte[65536];
            int count;
            while ((count = await source.ReadAsync(buffer, ct)) != 0)
            {
                length += count;
                if (length > maximumLength || length > declaredLength) throw new ValidationException("Document length does not match the upload.");
                digest.AppendData(buffer, 0, count);
                await destination.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            if (length != declaredLength) throw new ValidationException("Document length does not match the upload.");
            await destination.FlushAsync(ct);
            destination.Flush(flushToDisk: true);
        }
        return new(key, version, Convert.ToHexString(digest.GetHashAndReset()), length, contentType);
    }

    public Task PublishAsync(Guid tenantId, PrivateDocumentObject item, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var staged = ObjectPath(tenantId, item.Key, staging: true);
        var active = ObjectPath(tenantId, item.Key, staging: false);
        RejectLinks(staged);
        RejectLinks(Path.GetDirectoryName(active)!);
        Directory.CreateDirectory(Path.GetDirectoryName(active)!);
        RejectLinks(staged);
        RejectLinks(Path.GetDirectoryName(active)!);
        File.Move(staged, active, overwrite: false);
        return Task.CompletedTask;
    }

    public async Task<Stream> OpenReadAsync(Guid tenantId, string key, string sha256, long length, CancellationToken ct)
    {
        var path = ObjectPath(tenantId, key, staging: false);
        RejectLinks(path);
        if (!File.Exists(path)) throw new NotFoundException("Document file");
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous);
        try
        {
            if (stream.Length != length || length <= 0) throw new DomainException("Document integrity verification failed.", 409);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            if (!string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase)) throw new DomainException("Document integrity verification failed.", 409);
            stream.Position = 0;
            return stream;
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    public Task<Stream> OpenLegacyReadAsync(Guid patientId, string fileUrl, bool labReport, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var folder = labReport ? options.Value.LabReportsPath : options.Value.PatientDocumentsPath;
        if (Path.IsPathRooted(folder) || folder.Contains("..")) throw new InvalidOperationException("Legacy document directories must remain beneath the content root.");
        var expectedPrefix = $"/{folder.Trim('/')}/{patientId:N}/";
        if (!fileUrl.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) throw new DomainException("Legacy document mapping requires operator review.", 409);
        var name = fileUrl[expectedPrefix.Length..];
        if (name != Path.GetFileName(name) || name.Contains('\\') || name.Contains('/') || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(name), "N", out _))
            throw new DomainException("Legacy document mapping requires operator review.", 409);
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, folder, patientId.ToString("N"), name));
        RejectLinks(path);
        if (!File.Exists(path)) throw new NotFoundException("Legacy document file");
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task<IReadOnlyList<PrivateDocumentInventoryItem>> InventoryAsync(Guid tenantId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty) throw new DomainException("Tenant identity is required.", 403);
        var items = new List<PrivateDocumentInventoryItem>();
        foreach (var area in new[] { "staging", "objects" })
        {
            var directory = Path.Combine(Root(), area, tenantId.ToString("N"));
            RejectLinks(directory);
            if (!Directory.Exists(directory)) continue;
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                ct.ThrowIfCancellationRequested();
                RejectLinks(path);
                var name = Path.GetFileName(path);
                if (!Guid.TryParseExact(name, "N", out _)) throw new InvalidOperationException("Unrecognized private object requires operator review.");
                items.Add(new($"{tenantId:N}/{name}", area == "staging"));
            }
        }
        return Task.FromResult<IReadOnlyList<PrivateDocumentInventoryItem>>(items);
    }

    private string ObjectPath(Guid tenantId, string key, bool staging)
    {
        var parts = key.Split('/');
        if (tenantId == Guid.Empty || parts.Length != 2 || parts[0] != tenantId.ToString("N") || !Guid.TryParseExact(parts[1], "N", out _))
            throw new DomainException("Document object scope does not match.", 403);
        return Path.Combine(Root(), staging ? "staging" : "objects", parts[0], parts[1]);
    }

    private static void RejectLinks(string path)
    {
        // FileInfo.Exists is false for directories. Query attributes directly for
        // both files and directories, including every existing ancestor before creation.
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Document storage paths must not contain symbolic links or reparse points.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
    }
}