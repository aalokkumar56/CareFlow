using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Middleware;

public static class ApiProblems
{
    public const int MaximumErrors = 64;

    public static Dictionary<string, string[]> FieldErrors(IEnumerable<ValidationFailure> failures) =>
        failures.Take(MaximumErrors)
            .GroupBy(f => JsonNamingPolicy.SnakeCaseLower.ConvertName(f.PropertyName))
            .ToDictionary(g => g.Key, g => g.Take(4).Select(f => f.ErrorCode switch
            {
                "NotEmptyValidator" or "NotNullValidator" => "This field is required.",
                "MaximumLengthValidator" or "LengthValidator" => "Value exceeds the permitted length.",
                "MinimumLengthValidator" => "Value is shorter than the permitted length.",
                "EmailValidator" => "Enter a valid email address.",
                "InclusiveBetweenValidator" => "Value is outside the permitted range.",
                _ => "Value does not meet the field requirements."
            }).Distinct().ToArray());

    public static ProblemDetails Create(HttpContext context, int status, string detail,
        IDictionary<string, string[]>? errors = null)
    {
        if (!context.Response.HasStarted) context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        ProblemDetails problem = errors is null ? new ProblemDetails() : new ValidationProblemDetails(errors);
        problem.Status = status;
        problem.Type = "about:blank";
        problem.Title = status switch
        {
            400 or 422 => "Request validation failed",
            401 => "Authentication required",
            403 => "Access denied",
            404 => "Resource not found",
            409 => "Conflict",
            412 => "Precondition failed",
            413 => "Request too large",
            428 => "Precondition required",
            429 => "Too many requests",
            499 => "Request cancelled",
            _ => "Request failed"
        };
        problem.Detail = detail;
        // Do not echo request paths, query values, exception data or clinical text.
        problem.Extensions["code"] = status switch
        {
            400 or 422 => "validation_failed",
            401 => "authentication_required",
            403 => "access_denied",
            404 => "not_found",
            409 => "conflict",
            412 => "precondition_failed",
            413 => "request_too_large",
            428 => "precondition_required",
            429 => "rate_limited",
            499 => "request_cancelled",
            >= 500 => "internal_server_error",
            _ => "request_failed"
        };
        problem.Extensions["trace_id"] = context.TraceIdentifier;
        problem.Extensions["error"] = detail; // Preserve existing BFF error feedback.
        return problem;
    }

    public static ObjectResult Result(ProblemDetails problem)
    {
        var result = new ObjectResult(problem) { StatusCode = problem.Status };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        return context.Response.WriteAsJsonAsync(problem, options: (JsonSerializerOptions?)null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
