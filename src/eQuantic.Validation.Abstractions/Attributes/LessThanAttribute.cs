namespace eQuantic.Validation.Attributes;

/// <summary>Requires a numeric or comparable value to be strictly less than a maximum.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class LessThanAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="LessThanAttribute"/> for floating-point values.</summary>
    /// <param name="value">The maximum threshold.</param>
    public LessThanAttribute(double value)
        : base(ValidationCodes.Range, "{Property} must be less than {Value}.")
    {
        Value = value;
    }

    /// <summary>Initializes a new instance of <see cref="LessThanAttribute"/> for integer values.</summary>
    /// <param name="value">The maximum threshold.</param>
    public LessThanAttribute(long value)
        : base(ValidationCodes.Range, "{Property} must be less than {Value}.")
    {
        Value = value;
    }

    /// <summary>Gets the maximum threshold.</summary>
    public object Value { get; }
}
