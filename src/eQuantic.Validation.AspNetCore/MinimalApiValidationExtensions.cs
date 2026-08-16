using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Opt-in validation filters for ASP.NET Core Minimal APIs.</summary>
public static class MinimalApiValidationExtensions
{
    /// <summary>
    /// Validates registered endpoint arguments before the handler executes and returns an RFC
    /// 7807 validation response when blocking issues are found.
    /// </summary>
    public static RouteHandlerBuilder RequireValidation(
        this RouteHandlerBuilder builder,
        params string[] scenarios)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var scenarioCopy = scenarios?.Where(static scenario => !string.IsNullOrWhiteSpace(scenario)).ToArray()
            ?? Array.Empty<string>();

        return builder.AddEndpointFilterFactory((_, next) => async invocationContext =>
        {
            var dispatcher = invocationContext.HttpContext.RequestServices.GetRequiredService<IValidationDispatcher>();
            var context = new ValidationContext(
                scenarios: scenarioCopy,
                services: invocationContext.HttpContext.RequestServices,
                executionMode: ValidationExecutionMode.Parallel);

            foreach (var argument in invocationContext.Arguments)
            {
                if (argument is null)
                {
                    continue;
                }

                var result = await dispatcher
                    .ValidateAsync(argument, invocationContext.HttpContext.RequestServices, context, invocationContext.HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                if (!result.IsValid)
                {
                    return ValidationProblemDetailsFactory.MinimalApi(result);
                }
            }

            return await next(invocationContext).ConfigureAwait(false);
        });
    }

    /// <summary>Applies validation to every endpoint added to a route group.</summary>
    public static RouteGroupBuilder RequireValidation(
        this RouteGroupBuilder builder,
        params string[] scenarios)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        var scenarioCopy = scenarios?.Where(static scenario => !string.IsNullOrWhiteSpace(scenario)).ToArray()
            ?? Array.Empty<string>();

        return builder.AddEndpointFilterFactory((_, next) => async invocationContext =>
        {
            var dispatcher = invocationContext.HttpContext.RequestServices.GetRequiredService<IValidationDispatcher>();
            var context = new ValidationContext(
                scenarios: scenarioCopy,
                services: invocationContext.HttpContext.RequestServices,
                executionMode: ValidationExecutionMode.Parallel);

            foreach (var argument in invocationContext.Arguments)
            {
                if (argument is null)
                {
                    continue;
                }

                var result = await dispatcher
                    .ValidateAsync(argument, invocationContext.HttpContext.RequestServices, context, invocationContext.HttpContext.RequestAborted)
                    .ConfigureAwait(false);
                if (!result.IsValid)
                {
                    return ValidationProblemDetailsFactory.MinimalApi(result);
                }
            }

            return await next(invocationContext).ConfigureAwait(false);
        });
    }
}
