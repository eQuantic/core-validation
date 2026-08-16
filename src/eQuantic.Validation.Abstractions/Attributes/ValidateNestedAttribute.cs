namespace eQuantic.Validation.Attributes;

/// <summary>
/// Instructs the source generator to validate a complex nested object property using its registered validator.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class ValidateNestedAttribute : Attribute
{
    /// <summary>Gets or sets the scenarios for which this nested validation applies.</summary>
    public string[]? Scenarios { get; set; }
}
