using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Opt-in integration for ASP.NET Core MVC controllers.</summary>
public static class MvcBuilderExtensions
{
    /// <summary>
    /// Registers the global MVC validation action filter. Rules run sequentially by default;
    /// pass <see cref="ValidationExecutionMode.Parallel"/> only when every asynchronous rule
    /// dependency is safe for concurrent use (scoped EF Core contexts are not).
    /// </summary>
    public static IMvcBuilder AddValidationFilter(
        this IMvcBuilder builder,
        ValidationExecutionMode executionMode = ValidationExecutionMode.Sequential)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.AddValidationDispatcher();
        builder.Services.TryAddScoped(_ => new ValidationActionFilter(executionMode));
        builder.AddMvcOptions(options => options.Filters.AddService<ValidationActionFilter>());
        return builder;
    }
}
