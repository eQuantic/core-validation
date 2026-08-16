namespace eQuantic.Validation.Attributes;

/// <summary>
/// Base attribute for declarative validation rules with rich metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = true)]
public abstract class ValidationRuleAttribute : Attribute
{
    /// <summary>Initializes a validation rule attribute.</summary>
    protected ValidationRuleAttribute(string defaultCode, string defaultTemplate)
    {
        Code = defaultCode ?? throw new ArgumentNullException(nameof(defaultCode));
        Message = defaultTemplate ?? throw new ArgumentNullException(nameof(defaultTemplate));
    }

    /// <summary>Gets or sets the stable machine-readable error code.</summary>
    public string Code { get; set; }

    /// <summary>Gets or sets the error message template. Placeholders like {Property}, {MinimumLength}, etc. are supported.</summary>
    public string Message { get; set; }

    /// <summary>Gets or sets the severity of the failure.</summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>Gets or sets the scenarios for which this rule applies. If null or empty, applies to all.</summary>
    public string[]? Scenarios { get; set; }
}
