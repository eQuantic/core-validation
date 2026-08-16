using System.Collections.ObjectModel;

namespace eQuantic.Validation;

/// <summary>Describes a failed rule before its user-facing message is resolved.</summary>
public sealed class ValidationMessageDescriptor
{
    /// <summary>Initializes a message descriptor.</summary>
    public ValidationMessageDescriptor(
        string path,
        string displayName,
        string code,
        string template,
        IReadOnlyDictionary<string, object?> arguments)
    {
        Path = path;
        DisplayName = displayName;
        Code = code;
        Template = template;
        Arguments = new ReadOnlyDictionary<string, object?>(arguments.ToDictionary(
            static argument => argument.Key,
            static argument => argument.Value,
            StringComparer.Ordinal));
    }

    /// <summary>Gets the canonical member path.</summary>
    public string Path { get; }

    /// <summary>Gets the display name used in default messages.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the stable machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the default message template.</summary>
    public string Template { get; }

    /// <summary>Gets named values that can be used by a localized template.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }
}
