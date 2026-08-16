using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Global MVC action filter that validates registered action arguments.</summary>
public sealed class ValidationActionFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly ValidationExecutionMode _executionMode;

    /// <summary>Initializes the filter with sequential rule execution.</summary>
    public ValidationActionFilter()
        : this(ValidationExecutionMode.Sequential)
    {
    }

    /// <summary>Initializes the filter with an explicit execution mode.</summary>
    public ValidationActionFilter(ValidationExecutionMode executionMode) => _executionMode = executionMode;

    /// <summary>Runs before the action body and after model binding.</summary>
    public int Order => -2_000;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var dispatcher = context.HttpContext.RequestServices.GetRequiredService<IValidationDispatcher>();
        var validationContext = new ValidationContext(
            services: context.HttpContext.RequestServices,
            executionMode: _executionMode,
            deduplicateAsyncRules: true);

        var warnings = new List<ValidationFailure>();
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

            warnings.AddRange(result.Warnings);
        }

        ValidationWarnings.Attach(context.HttpContext, warnings);
        await next().ConfigureAwait(false);
    }
}
