namespace eQuantic.Validation.Attributes;

/// <summary>Requires a string or collection length to be inside an inclusive range.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class LengthAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="LengthAttribute"/>.</summary>
    /// <param name="minimumLength">The minimum allowed length.</param>
    /// <param name="maximumLength">The maximum allowed length.</param>
    public LengthAttribute(int minimumLength, int maximumLength)
        : base(ValidationCodes.Range, "{Property} must contain between {MinimumLength} and {MaximumLength} characters.")
    {
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
    }

    /// <summary>Gets the minimum allowed length.</summary>
    public int MinimumLength { get; }

    /// <summary>Gets the maximum allowed length.</summary>
    public int MaximumLength { get; }
}
