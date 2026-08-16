using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eQuantic.Validation;

/// <summary>
/// Dependency injection registration for validation services. Lives in the core package so
/// workers, gRPC services and console apps can use validators without referencing ASP.NET Core.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the validation dispatcher. Named after what it registers, which also avoids
    /// colliding with the built-in <c>AddValidation</c> from Microsoft.Extensions.Validation in .NET 10.
    /// </summary>
    public static IServiceCollection AddValidationDispatcher(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<ValidationDispatcher>(serviceProvider =>
            new ValidationDispatcher(serviceProvider.GetServices<ValidationRegistration>()));
        services.TryAddSingleton<IValidationDispatcher>(serviceProvider =>
            serviceProvider.GetRequiredService<ValidationDispatcher>());
        return services;
    }

    /// <summary>
    /// Registers a validator as scoped. Explicit registration avoids assembly scanning and works
    /// cleanly with trimming, Native AOT and feature-sliced applications.
    /// </summary>
    public static IServiceCollection AddValidator<TModel,
#if NET8_0_OR_GREATER
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        TValidator>(this IServiceCollection services)
        where TValidator : class, IValidator<TModel>
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.AddValidationDispatcher();
        services.TryAddScoped<TValidator>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IValidator<TModel>, TValidator>());
        services.AddSingleton(new ValidationRegistration(
            typeof(TModel),
            serviceProvider => serviceProvider.GetRequiredService<TValidator>()));

        return services;
    }
}
