using System.Diagnostics;
using System.Security.Claims;
using CureFlow.Application.Interfaces;
using CureFlow.Domain.Entities.Saas;
using Microsoft.AspNetCore.Routing;

namespace CureFlow.Api.Middleware;

/// <summary>Place before exception handling so handled failures are included in the final status.</summary>
public sealed class TenantApiActivityMiddleware(
    RequestDelegate next,
    IServiceScopeFactory scopeFactory,
    ILogger<TenantApiActivityMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var correlationId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        using var diagnosticScope = logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId });
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-ID"] = correlationId;
            return Task.CompletedTask;
        });

        Exception? failure = null;
        try { await next(context); }
        catch (Exception exception) { failure = exception; throw; }
        finally
        {
            if (context.User.Identity?.IsAuthenticated == true
                && !context.User.HasClaim("platform_user", "true")
                && context.Request.Path.StartsWithSegments("/api")
                && !context.Request.Path.StartsWithSegments("/api/platform")
                && Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var tenantId)
                && tenantId != Guid.Empty)
            {
                var status = failure is OperationCanceledException ? 499
                    : failure is not null ? 500 : context.Response.StatusCode;
                // Route templates preserve the API identity without exposing IDs, searches, or clinical content.
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(unmatched API route)";
                Guid? actorId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub"), out var actor) ? actor : null;
                var entry = new TenantApiActivity
                {
                    TenantId = tenantId,
                    ActorId = actorId,
                    Method = context.Request.Method,
                    Route = route,
                    StatusCode = status,
                    DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                    CorrelationId = correlationId,
                    ErrorCode = status >= 400 ? $"HTTP_{status}" : null,
                    ErrorSummary = status switch
                    {
                        400 => "Request validation failed.",
                        401 => "Authentication required.",
                        403 => "Access denied.",
                        404 => "Resource or endpoint not found.",
                        409 => "Request conflicts with current state.",
                        422 => "Request could not be processed.",
                        429 => "Request rate limit exceeded.",
                        499 => "Request cancelled.",
                        >= 500 => "Server error. Use the correlation ID to locate server diagnostics.",
                        >= 400 => "Request failed.",
                        _ => null
                    }
                };
                logger.LogInformation("Tenant API {TenantId} {ActorId} {Method} {Route} returned {StatusCode} in {DurationMs}ms; correlation {CorrelationId}",
                    entry.TenantId, entry.ActorId, entry.Method, entry.Route, entry.StatusCode, entry.DurationMs, correlationId);
                try
                {
                    // The request session may have an aborted transaction. Audit using a fresh session.
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var auditContext = (CureFlow.Application.Common.CurrentTenant)scope.ServiceProvider.GetRequiredService<CureFlow.Application.Common.ITenantContext>();
                    auditContext.TenantId = entry.TenantId;
                    auditContext.UserId = entry.ActorId;
                    auditContext.IsAuthenticated = true;
                    var db = scope.ServiceProvider.GetRequiredService<ICureFlowDbSession>();
                    await db.InsertAsync(entry, ignoreTenant: true, ct: CancellationToken.None);
                }
                catch (Exception auditFailure)
                {
                    // Logging failure must never replace the original response or request exception.
                    logger.LogWarning("Tenant API audit persistence failed ({ErrorType}); correlation {CorrelationId}",
                        auditFailure.GetType().Name, correlationId);
                }
            }
        }
    }
}
