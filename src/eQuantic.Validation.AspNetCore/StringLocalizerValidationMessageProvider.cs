using Microsoft.Extensions.Localization;

namespace eQuantic.Validation.AspNetCore;

/// <summary>
/// Resolves localized validation messages using <see cref="IStringLocalizer"/>.
/// </summary>
public sealed class StringLocalizerValidationMessageProvider : IValidationMessageProvider
{
    private readonly IStringLocalizer? _localizer;

    /// <summary>Initializes a new instance using an existing localizer.</summary>
    public StringLocalizerValidationMessageProvider(IStringLocalizer localizer)
    {
        _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
    }

    /// <summary>Initializes a new instance using a factory and resource type.</summary>
    public StringLocalizerValidationMessageProvider(IStringLocalizerFactory factory, Type resourceType)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        if (resourceType is null) throw new ArgumentNullException(nameof(resourceType));
        _localizer = factory.Create(resourceType);
    }

    /// <summary>Initializes a new instance using a factory, base name and location.</summary>
    public StringLocalizerValidationMessageProvider(IStringLocalizerFactory factory, string baseName, string location)
    {
        if (factory is null) throw new ArgumentNullException(nameof(factory));
        _localizer = factory.Create(baseName, location);
    }

    /// <inheritdoc />
    public string Resolve(ValidationMessageDescriptor descriptor)
    {
        if (descriptor is null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        if (_localizer is null)
        {
            return descriptor.Template;
        }

        // 1. Try lookup by stable Code (e.g. "custom.email.invalid")
        if (!string.IsNullOrEmpty(descriptor.Code))
        {
            var localizedByCode = _localizer[descriptor.Code];
            if (!localizedByCode.ResourceNotFound)
            {
                return localizedByCode.Value;
            }
        }

        // 2. Try lookup by Template (e.g. "{Property} is required.")
        var localizedByTemplate = _localizer[descriptor.Template];
        if (!localizedByTemplate.ResourceNotFound)
        {
            return localizedByTemplate.Value;
        }

        return descriptor.Template;
    }
}
