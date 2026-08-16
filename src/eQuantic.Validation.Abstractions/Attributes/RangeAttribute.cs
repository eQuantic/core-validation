namespace eQuantic.Validation.Attributes;

/// <summary>Requires a numeric or comparable value to be within an inclusive range.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class RangeAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="RangeAttribute"/> for floating-point values.</summary>
    /// <param name="minimum">The minimum allowed value.</param>
    /// <param name="maximum">The maximum allowed value.</param>
    public RangeAttribute(double minimum, double maximum)
        : base(ValidationCodes.Range, "{Property} must be between {Minimum} and {Maximum}.")
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Initializes a new instance of <see cref="RangeAttribute"/> for integer values.</summary>
    /// <param name="minimum">The minimum allowed value.</param>
    /// <param name="maximum">The maximum allowed value.</param>
    public RangeAttribute(long minimum, long maximum)
        : base(ValidationCodes.Range, "{Property} must be between {Minimum} and {Maximum}.")
    {
        Minimum = minimum;
        Maximum = maximum;
    }

    /// <summary>Gets the minimum allowed value.</summary>
    public object Minimum { get; }

    /// <summary>Gets the maximum allowed value.</summary>
    public object Maximum { get; }
}
