using CureFlow.Application.Areas.Platform;
using CureFlow.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CureFlow.Tests;

public class PlatformRequestValidationTests
{
    [Theory]
    [InlineData("Plan updated", 1, true)]
    [InlineData("", 1, false)]
    [InlineData("ab", 1, false)]
    [InlineData("Plan updated", 0, false)]
    public void Subscription_validation_uses_constructor_constraints(string reason, int seats, bool valid)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();
        var context = new ActionContext(new DefaultHttpContext { RequestServices = provider },
            new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var input = new SubscriptionInput(Enum.GetValues<SubscriptionPlan>()[0],
            Enum.GetValues<SubscriptionStatus>()[0], seats, 1, 0, null, null, reason);

        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, "", input);

        Assert.Equal(valid, context.ModelState.IsValid);
        if (!valid)
            Assert.Contains(context.ModelState, entry => entry.Key == (seats == 0 ? "SeatLimit" : "Reason")
                && entry.Value!.Errors.Count > 0);
    }
}