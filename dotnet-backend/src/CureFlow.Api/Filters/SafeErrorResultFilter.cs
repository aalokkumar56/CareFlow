using CureFlow.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CureFlow.Api.Filters;

public sealed class SafeErrorResultFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var status = context.Result switch
        {
            ObjectResult result => result.StatusCode ?? (result.Value as ProblemDetails)?.Status,
            StatusCodeResult result => result.StatusCode,
            _ => null
        };
        if (status is not >= 400) return;
        if (context.Result is ObjectResult { Value: ProblemDetails problem }
            && problem.Extensions.ContainsKey("code")) return;
        context.Result = ApiProblems.Result(ApiProblems.Create(context.HttpContext, status.Value,
            status switch
            {
                401 => "Sign in to continue.",
                403 => "You do not have permission to perform this action.",
                404 => "The requested resource was not found.",
                409 => "The request conflicts with the current state. Reload before trying again.",
                412 => "The record changed. Review the latest version before saving.",
                428 => "Supply the current record version before saving.",
                429 => "Too many requests. Try again later.",
                >= 500 => "Internal server error.",
                _ => "The request could not be processed. Check your input."
            }));
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
