using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Global MVC action filter that validates registered action arguments.</summary>
public sealed class ValidationActionFilter : IAsyncActionFilter, IOrderedFilter
{
    /// <summary>Runs before the action body and after model binding.</summary>
    public int Order => -2_000;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var dispatcher = context.HttpContext.RequestServices.GetRequiredService<IValidationDispatcher>();
        var validationContext = new ValidationContext(
            services: context.HttpContext.RequestServices,
            executionMode: ValidationExecutionMode.Parallel);

        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var result = await dispatcher
                .ValidateAsync(argument, context.HttpContext.RequestServices, validationContext, context.HttpContext.RequestAborted)
                .ConfigureAwait(false);
            if (!result.IsValid)
            {
                context.Result = new BadRequestObjectResult(ValidationProblemDetailsFactory.Mvc(result));
                return;
            }
        }

        await next().ConfigureAwait(false);
    }
}
