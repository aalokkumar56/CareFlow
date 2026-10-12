using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CureFlow.Application.DTOs;
using CureFlow.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CureFlow.Api.Filters;

public sealed class AuthenticationAttemptBudget : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter;
    public AuthenticationAttemptBudget(IConfiguration config)
    {
        var permits = Math.Clamp(config.GetValue<int?>("RateLimiting:AccountAttemptsPerMinute") ?? 20, 1, 100);
        _limiter = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new() { Window = TimeSpan.FromMinutes(1), PermitLimit = permits, QueueLimit = 0 }));
    }
    public RateLimitLease Acquire(string key) => _limiter.AttemptAcquire(key);
    public void Dispose() => _limiter.Dispose();
}

public sealed class AuthenticationAttemptFilter(AuthenticationAttemptBudget budget) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var login = context.ActionArguments.Values.OfType<LoginRequest>().FirstOrDefault();
        var bootstrap = context.ActionArguments.Values.OfType<PlatformBootstrapRequest>().FirstOrDefault();
        var email = login?.Email ?? bootstrap?.Email;
        if (email is not null)
        {
            var ctx = context.HttpContext;
            var platform = ctx.Request.Path.StartsWithSegments("/api/platform");
            var tenant = ctx.Items["resolved_tenant_id"]?.ToString() ?? login?.TenantSlug?.Trim().ToLowerInvariant() ?? "unspecified";
            // The key is independent of account existence. Do not persist/log submitted account values.
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                (platform ? "platform" : "hospital:" + tenant) + "\n" + email.Trim().ToLowerInvariant())));
            using var lease = budget.Acquire(key);
            if (!lease.IsAcquired)
            {
                ctx.Response.Headers.RetryAfter = "60";
                context.Result = ApiProblems.Result(ApiProblems.Create(ctx, 429, "Too many requests. Wait before trying again."));
                return;
            }
        }
        await next();
    }
}
