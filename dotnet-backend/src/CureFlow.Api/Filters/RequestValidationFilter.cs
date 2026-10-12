using FluentValidation;
using CureFlow.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CureFlow.Api.Filters;

public sealed class RequestValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;
            var validation = await validator.ValidateAsync(new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);
            if (!validation.IsValid)
            {
                context.Result = ApiProblems.Result(ApiProblems.Create(context.HttpContext, 422,
                    "Correct the indicated fields and try again.", ApiProblems.FieldErrors(validation.Errors)));
                return;
            }
        }
        await next();
    }
}
