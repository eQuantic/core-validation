using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Localization registration for validation messages.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a localized validation message provider using <see cref="IStringLocalizer{TResource}"/>.
    /// Combine with request localization middleware so the request culture drives the resolved language.
    /// </summary>
    public static IServiceCollection AddValidationLocalization<TResource>(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddValidationDispatcher();
        services.TryAddSingleton<IValidationMessageProvider>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            return new StringLocalizerValidationMessageProvider(factory, typeof(TResource));
        });

        return services;
    }

    /// <summary>
    /// Registers a localized validation message provider using an existing <see cref="IStringLocalizer"/>.
    /// </summary>
    public static IServiceCollection AddValidationLocalization(this IServiceCollection services, IStringLocalizer localizer)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (localizer is null)
        {
            throw new ArgumentNullException(nameof(localizer));
        }

        services.AddValidationDispatcher();
        services.TryAddSingleton<IValidationMessageProvider>(new StringLocalizerValidationMessageProvider(localizer));
        return services;
    }
}
