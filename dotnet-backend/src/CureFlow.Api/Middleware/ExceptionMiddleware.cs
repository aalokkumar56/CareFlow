using CureFlow.Application.Common;

namespace CureFlow.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DomainException de)
        {
            ctx.Response.StatusCode = de.StatusCode;
            await ctx.Response.WriteAsJsonAsync(new { error = de.Message, detail = de.Message });
        }
        catch (OperationCanceledException) when (!ctx.Response.HasStarted)
        {
            ctx.Response.StatusCode = 499;
            await ctx.Response.WriteAsJsonAsync(new { error = "Request cancelled", detail = "Request cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = 500;
            var message = "Internal server error";
            await ctx.Response.WriteAsJsonAsync(new { error = message, detail = message });
        }
    }
}
