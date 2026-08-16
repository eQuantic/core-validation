using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Opt-in integration for ASP.NET Core MVC controllers.</summary>
public static class MvcBuilderExtensions
{
    /// <summary>Registers validation as a global MVC action filter.</summary>
    public static IMvcBuilder AddValidation(this IMvcBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.AddValidation();
        builder.Services.TryAddScoped<ValidationActionFilter>();
        builder.AddMvcOptions(options => options.Filters.AddService<ValidationActionFilter>());
        return builder;
    }
}
