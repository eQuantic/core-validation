using System.Collections.ObjectModel;

namespace eQuantic.Validation;

/// <summary>A single structured validation issue.</summary>
public sealed class ValidationFailure
{
    /// <summary>Initializes a validation failure.</summary>
    public ValidationFailure(
        string path,
        string code,
        string message,
        ValidationSeverity severity = ValidationSeverity.Error,
        IReadOnlyDictionary<string, object?>? arguments = null)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Severity = severity;
        Arguments = arguments is null
            ? new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>())
            : new ReadOnlyDictionary<string, object?>(arguments.ToDictionary(
                static argument => argument.Key,
                static argument => argument.Value,
                StringComparer.Ordinal));
    }

    /// <summary>Gets the canonical property path, such as <c>Address.PostalCode</c>.</summary>
    public string Path { get; }

    /// <summary>Gets a stable machine-readable error code.</summary>
    public string Code { get; }

    /// <summary>Gets the localized, user-facing message.</summary>
    public string Message { get; }

    /// <summary>Gets the issue severity.</summary>
    public ValidationSeverity Severity { get; }

    /// <summary>Gets safe rule arguments, for example <c>MinLength</c>.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>Creates the same issue below a parent member path.</summary>
    public ValidationFailure WithPathPrefix(string prefix)
    {
        var path = string.IsNullOrEmpty(Path)
            ? prefix
            : string.IsNullOrEmpty(prefix) ? Path : prefix + "." + Path;

        return new ValidationFailure(path, Code, Message, Severity, Arguments);
    }
}
