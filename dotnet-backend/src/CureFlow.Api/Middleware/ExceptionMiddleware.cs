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
        catch (BadHttpRequestException badRequest) when (!ctx.Response.HasStarted)
        {
            var status = badRequest.StatusCode == 413 ? 413 : 400;
            await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, status,
                status == 413 ? "Request exceeds the permitted size. Send a smaller request." : "Invalid request."));
        }
        catch (FluentValidation.ValidationException validation) when (!ctx.Response.HasStarted)
        {
            await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, 422,
                "Correct the indicated fields and try again.", ApiProblems.FieldErrors(validation.Errors)));
        }
        catch (DomainException de) when (!ctx.Response.HasStarted)
        {
            await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, de.StatusCode, de.Message));
        }
        catch (OperationCanceledException) when (!ctx.Response.HasStarted)
        {
            if (ctx.Features.Get<Microsoft.AspNetCore.Http.Timeouts.IHttpRequestTimeoutFeature>()?.RequestTimeoutToken.IsCancellationRequested == true)
                throw; // The outer timeout middleware restores the transport token before writing its safe 504 response.
            else if (!ctx.RequestAborted.IsCancellationRequested)
                await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, 499, "Request cancelled."));
            else
                ctx.Response.StatusCode = 499;
        }
        catch (Npgsql.PostgresException dbError) when (!ctx.Response.HasStarted)
        {
            var (status, detail) = dbError.SqlState switch
            {
                "23505" => (409, "A record with these values already exists."),
                "23P01" => (409, "The requested resource allocation is unavailable. Review availability before retrying."),
                "23503" => (422, "A referenced record is invalid or unavailable."),
                "23514" => (422, "The request does not meet record constraints."),
                "40001" or "40P01" => (409, "A concurrent change prevented this request. Review the current state before retrying."),
                _ => (500, "Internal server error.")
            };
            _logger.LogWarning("Database request failed with {SqlState}; trace {TraceId}", dbError.SqlState, ctx.TraceIdentifier);
            await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, status, detail));
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            _logger.LogError("Unhandled {ExceptionType}; trace {TraceId}", ex.GetType().Name, ctx.TraceIdentifier);
            await ApiProblems.WriteAsync(ctx, ApiProblems.Create(ctx, 500, "Internal server error."));
        }
    }
}
