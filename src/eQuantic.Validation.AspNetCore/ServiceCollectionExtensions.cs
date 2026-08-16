using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace eQuantic.Validation.AspNetCore;

/// <summary>Dependency injection registration for validation services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the validation dispatcher.</summary>
    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<ValidationDispatcher>(serviceProvider =>
            new ValidationDispatcher(serviceProvider.GetServices<ValidationRegistration>()));
        services.TryAddSingleton<IValidationDispatcher>(serviceProvider => serviceProvider.GetRequiredService<ValidationDispatcher>());
        return services;
    }

    /// <summary>
    /// Registers a validator as scoped. Explicit registration avoids assembly scanning and works
    /// cleanly with trimming, Native AOT and feature-sliced applications.
    /// </summary>
    public static IServiceCollection AddValidator<TModel, TValidator>(this IServiceCollection services)
        where TValidator : class, IValidator<TModel>
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddValidation();
        services.TryAddScoped<TValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator<TModel>, TValidator>());
        services.AddSingleton(new ValidationRegistration(
            typeof(TModel),
            serviceProvider => serviceProvider.GetRequiredService<TValidator>()));

        return services;
    }

    /// <summary>
    /// Registers a localized validation message provider using <see cref="IStringLocalizer{TResource}"/>.
    /// </summary>
    public static IServiceCollection AddValidationLocalization<TResource>(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddValidation();
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

        services.AddValidation();
        services.TryAddSingleton<IValidationMessageProvider>(new StringLocalizerValidationMessageProvider(localizer));
        return services;
    }
}
