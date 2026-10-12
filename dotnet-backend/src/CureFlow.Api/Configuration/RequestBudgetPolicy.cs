using System.Security.Claims;
using System.Threading.RateLimiting;
using CureFlow.Api.Middleware;
using Microsoft.AspNetCore.RateLimiting;

namespace CureFlow.Api.Configuration;

public static class RequestBudgetPolicy
{
    public static void Configure(RateLimiterOptions options, IConfiguration config, bool relaxed)
    {
        int Read(string key, int fallback, int maximum) => Math.Clamp(config.GetValue<int?>("RateLimiting:" + key) ?? fallback, 1, maximum);
        var actorAttempts = Read("ActorRequestsPerMinute", 300, 6000);
        var tenantAttempts = Read("TenantRequestsPerMinute", 1200, 24000);
        var networkAttempts = Read("NetworkRequestsPerMinute", 120, 2400);
        var actorConcurrency = Read("ActorConcurrency", 8, 64);
        var tenantConcurrency = Read("TenantConcurrency", 32, 128);
        var expensiveConcurrency = Read("ExpensiveActorConcurrency", 2, 8);
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, _) =>
        {
            var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? retry.TotalSeconds : 5;
            context.HttpContext.Response.Headers.RetryAfter = Math.Clamp((int)Math.Ceiling(seconds), 1, 600).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ApiProblems.WriteAsync(context.HttpContext, ApiProblems.Create(context.HttpContext, 429, "Too many requests. Wait before trying again."));
        };
        options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(ctx => Exempt(ctx)
                ? RateLimitPartition.GetNoLimiter("health")
                : RateLimitPartition.GetFixedWindowLimiter(TenantKey(ctx), _ => new()
                {
                    Window = TimeSpan.FromMinutes(1), PermitLimit = relaxed ? 24000 : IsTenant(ctx) ? tenantAttempts : networkAttempts, QueueLimit = 0
                })),
            PartitionedRateLimiter.Create<HttpContext, string>(ctx => Exempt(ctx)
                ? RateLimitPartition.GetNoLimiter("health")
                : RateLimitPartition.GetFixedWindowLimiter(ActorKey(ctx), _ => new()
                {
                    Window = TimeSpan.FromMinutes(1), PermitLimit = relaxed ? 6000 : actorAttempts, QueueLimit = 0
                })),
            PartitionedRateLimiter.Create<HttpContext, string>(ctx => Exempt(ctx)
                ? RateLimitPartition.GetNoLimiter("health")
                : RateLimitPartition.GetConcurrencyLimiter(TenantKey(ctx), _ => new() { PermitLimit = tenantConcurrency, QueueLimit = 0 })),
            PartitionedRateLimiter.Create<HttpContext, string>(ctx => Exempt(ctx)
                ? RateLimitPartition.GetNoLimiter("health")
                : RateLimitPartition.GetConcurrencyLimiter(ActorKey(ctx) + (Expensive(ctx) ? ":expensive" : ":normal"), _ => new()
                { PermitLimit = Expensive(ctx) ? expensiveConcurrency : actorConcurrency, QueueLimit = 0 })));
        Add("login", Read("LoginAttemptsPerMinute", relaxed ? 1000 : 10, relaxed ? 1000 : 100), 1);
        Add("register-tenant", Read("RegistrationAttemptsPerTenMinutes", relaxed ? 100 : 3, 100), 10);
        Add("platform-bootstrap", Read("BootstrapAttemptsPerTenMinutes", relaxed ? 100 : 3, 100), 10);
        void Add(string name, int permits, int minutes) => options.AddPolicy(name, ctx =>
            RateLimitPartition.GetFixedWindowLimiter(NetworkKey(ctx), _ => new() { Window = TimeSpan.FromMinutes(minutes), PermitLimit = permits, QueueLimit = 0 }));
    }

    // Only signed, validated bearer claims select authenticated quotas. Tenant headers and forwarded IP headers never do.
    public static string TenantKey(HttpContext ctx) => IsTenant(ctx) ? "tenant:" + ctx.User.FindFirstValue("tenant_id") : NetworkKey(ctx);
    public static string ActorKey(HttpContext ctx) => ctx.User.Identity?.IsAuthenticated == true
        ? TenantKey(ctx) + ":actor:" + (ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub") ?? "unknown")
        : NetworkKey(ctx);
    private static bool IsTenant(HttpContext ctx) => ctx.User.Identity?.IsAuthenticated == true && Guid.TryParse(ctx.User.FindFirstValue("tenant_id"), out var id) && id != Guid.Empty;
    private static string NetworkKey(HttpContext ctx) => "network:" + (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    private static bool Exempt(HttpContext ctx) => ctx.Request.Path.Equals(new PathString("/api/health")) || ctx.Request.Path.StartsWithSegments("/api/health/");
    public static bool Expensive(HttpContext ctx) => ctx.Request.Path.Value?.Contains("import", StringComparison.OrdinalIgnoreCase) == true || ctx.Request.Path.StartsWithSegments("/api/patient-exports") || ctx.Request.HasFormContentType || ctx.Request.Path.StartsWithSegments("/api/ai");
}
