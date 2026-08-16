namespace eQuantic.Validation.Attributes;

/// <summary>Requires a numeric or comparable value to be strictly greater than a minimum.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class GreaterThanAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="GreaterThanAttribute"/> for floating-point values.</summary>
    /// <param name="value">The minimum threshold.</param>
    public GreaterThanAttribute(double value)
        : base(ValidationCodes.Range, "{Property} must be greater than {Value}.")
    {
        Value = value;
    }

    /// <summary>Initializes a new instance of <see cref="GreaterThanAttribute"/> for integer values.</summary>
    /// <param name="value">The minimum threshold.</param>
    public GreaterThanAttribute(long value)
        : base(ValidationCodes.Range, "{Property} must be greater than {Value}.")
    {
        Value = value;
    }

    /// <summary>Gets the minimum threshold.</summary>
    public object Value { get; }
}
