using System.Diagnostics;
using System.Diagnostics.Metrics;
using CureFlow.Application.Options;
using CureFlow.Infrastructure.Persistence.Dapper;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CureFlow.Api.Middleware;

/// <summary>Health probes run before tenant resolution and never expose dependency details.</summary>
public sealed class DependencyHealthMiddleware(RequestDelegate next, IWebHostEnvironment environment)
{
    private static readonly Meter Meter = new("CureFlow.Readiness", "1.0.0");
    private static readonly Counter<long> Checks = Meter.CreateCounter<long>("cureflow.readiness.checks");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("cureflow.readiness.duration", "ms");
    // Bound probe concurrency even before the ordinary request limiter/identity pipeline.
    private static readonly SemaphoreSlim ReadyGate = new(1, 1);
    private static readonly SemaphoreSlim StorageGate = new(1, 1);
    private static long cacheUntilTicks;
    private static int cachedReady;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var live = path is "/api/health" or "/api/health/live";
        var ready = path is "/api/health/ready";
        if ((!live && !ready) || !HttpMethods.IsGet(context.Request.Method))
        {
            await next(context);
            return;
        }

        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (live)
        {
            await context.Response.WriteAsJsonAsync(new { status = "healthy", time = DateTime.UtcNow }, context.RequestAborted);
            return;
        }

        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref cacheUntilTicks))
        {
            var healthy = Volatile.Read(ref cachedReady) == 1;
            context.Response.StatusCode = healthy ? 200 : StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { status = healthy ? "ready" : "unready" }, context.RequestAborted);
            return;
        }

        if (!await ReadyGate.WaitAsync(0, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { status = "unready" }, context.RequestAborted);
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            var results = await Task.WhenAll(new[] { "Default", "Identity", "Catalogue", "PlatformIdentity", "ControlPlane" }
                .Select(name => ProbeAsync(name, async () =>
                {
                    var source = name == "Default"
                        ? context.RequestServices.GetRequiredService<NpgsqlDataSource>()
                        : context.RequestServices.GetRequiredKeyedService<NpgsqlDataSource>(name);
                    await RuntimeDependencyProbe.CheckDatabaseAsync(source, name, timeout.Token);
                })).Append(ProbeAsync("PrivateStorage", () => CheckPrivateStorageAsync(
                    context.RequestServices.GetRequiredService<IOptions<StorageOptions>>().Value.PrivateRoot,
                    timeout.Token))));
            var healthy = results.All(success => success);
            // Bound anonymous probe traffic to one dependency exercise per five seconds.
            Volatile.Write(ref cachedReady, healthy ? 1 : 0);
            Interlocked.Exchange(ref cacheUntilTicks, DateTime.UtcNow.AddSeconds(5).Ticks);
            context.Response.StatusCode = healthy ? 200 : StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { status = healthy ? "ready" : "unready" }, context.RequestAborted);
        }
        finally { ReadyGate.Release(); }
    }

    private async Task CheckPrivateStorageAsync(string? root, CancellationToken ct)
    {
        if (!await StorageGate.WaitAsync(0, ct))
            throw new InvalidOperationException("Private storage probe is busy.");
        // A hung network mount must not hold the response or accumulate background probes.
        // At most one filesystem worker remains active, even after the response deadline.
        var worker = Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
                    throw new InvalidOperationException("Private storage unavailable.");
                var privateRoot = Path.GetFullPath(root);
                var apiRoot = Path.GetFullPath(environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (privateRoot.Equals(apiRoot.TrimEnd(Path.DirectorySeparatorChar), comparison)
                    || privateRoot.StartsWith(apiRoot, comparison) || !Directory.Exists(privateRoot))
                    throw new InvalidOperationException("Private storage unavailable.");
                for (var directory = new DirectoryInfo(privateRoot); directory is not null; directory = directory.Parent)
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("Private storage unavailable.");
                // Read access only: never create files or expose object names.
                using var entries = Directory.EnumerateFileSystemEntries(privateRoot).GetEnumerator();
                entries.MoveNext();
            }
            finally { StorageGate.Release(); }
        });
        _ = worker.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await worker.WaitAsync(ct);
    }

    private static async Task<bool> ProbeAsync(string dependency, Func<Task> check)
    {
        var started = Stopwatch.GetTimestamp();
        var available = false;
        try { await check(); available = true; }
        catch (Exception) { /* A failed probe returns only a fixed status, never provider/credential details. */ }
        finally
        {
            var tags = new TagList { { "dependency", dependency }, { "outcome", available ? "ready" : "unready" } };
            Checks.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds, tags);
        }
        return available;
    }
}
