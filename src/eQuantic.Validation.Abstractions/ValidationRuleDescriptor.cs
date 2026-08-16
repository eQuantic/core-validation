using System.Collections.ObjectModel;

namespace eQuantic.Validation;

/// <summary>
/// A single validation rule described as data: what is checked, where, and with which parameters.
/// Produced by <see cref="IDescribableValidator.Describe"/> and consumed by schema enrichment
/// (OpenAPI), documentation and client-side schema exporters.
/// </summary>
public sealed class ValidationRuleDescriptor
{
    /// <summary>Initializes a rule descriptor.</summary>
    public ValidationRuleDescriptor(
        string path,
        string kind,
        string code,
        string messageTemplate,
        ValidationSeverity severity = ValidationSeverity.Error,
        IReadOnlyDictionary<string, object?>? arguments = null,
        IReadOnlyList<string>? scenarios = null,
        bool isAsync = false,
        bool isConditional = false,
        Type? valueType = null)
    {
        ValueType = valueType;
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Code = code ?? throw new ArgumentNullException(nameof(code));
        MessageTemplate = messageTemplate ?? throw new ArgumentNullException(nameof(messageTemplate));
        Severity = severity;
        Arguments = arguments is null || arguments.Count == 0
            ? EmptyArguments
            : new ReadOnlyDictionary<string, object?>(arguments.ToDictionary(
                static argument => argument.Key,
                static argument => argument.Value,
                StringComparer.Ordinal));
        Scenarios = scenarios is null || scenarios.Count == 0 ? EmptyScenarios : scenarios;
        IsAsync = isAsync;
        IsConditional = isConditional;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyArguments =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    private static readonly IReadOnlyList<string> EmptyScenarios = Array.Empty<string>();

    /// <summary>
    /// Gets the canonical member path: <c>"Email"</c>, <c>"Address.PostalCode"</c>,
    /// <c>"Contacts[]"</c> for collection elements, or <c>""</c> for model-level rules.
    /// </summary>
    public string Path { get; }

    /// <summary>Gets the stable rule kind (see <see cref="ValidationRuleKinds"/>). Unlike <see cref="Code"/>, the kind never changes with <c>WithCode</c>.</summary>
    public string Kind { get; }

    /// <summary>Gets the stable machine-readable error code reported on failure.</summary>
    public string Code { get; }

    /// <summary>Gets the raw message template, with placeholders unresolved.</summary>
    public string MessageTemplate { get; }

    /// <summary>Gets the failure severity.</summary>
    public ValidationSeverity Severity { get; }

    /// <summary>Gets the rule parameters, for example <c>MinimumLength = 2</c> or <c>Pattern</c>.</summary>
    public IReadOnlyDictionary<string, object?> Arguments { get; }

    /// <summary>Gets the scenarios the rule is restricted to; empty means it always applies.</summary>
    public IReadOnlyList<string> Scenarios { get; }

    /// <summary>Gets whether the rule requires asynchronous validation.</summary>
    public bool IsAsync { get; }

    /// <summary>Gets whether the rule is guarded by a runtime condition (<c>When</c>).</summary>
    public bool IsConditional { get; }

    /// <summary>Gets the CLR type of the validated value when known, for schema exporters.</summary>
    public Type? ValueType { get; }

    /// <summary>Creates the same descriptor re-rooted under a parent member path.</summary>
    public ValidationRuleDescriptor WithPathPrefix(string prefix, bool markConditional = false)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            throw new ArgumentException("A path prefix is required.", nameof(prefix));
        }

        var path = Path.Length == 0 ? prefix : prefix + "." + Path;
        return new ValidationRuleDescriptor(
            path, Kind, Code, MessageTemplate, Severity, Arguments, Scenarios,
            IsAsync, IsConditional || markConditional, ValueType);
    }
}
