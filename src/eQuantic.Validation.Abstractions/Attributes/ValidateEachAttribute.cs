namespace eQuantic.Validation.Attributes;

/// <summary>
/// Instructs the source generator to validate every item inside a collection property using its registered validator or rules.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class ValidateEachAttribute : Attribute
{
    /// <summary>Gets or sets the scenarios for which this collection element validation applies.</summary>
    public string[]? Scenarios { get; set; }
}
