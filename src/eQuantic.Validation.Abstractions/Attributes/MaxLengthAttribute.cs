namespace eQuantic.Validation.Attributes;

/// <summary>Requires a string or collection to contain no more than the specified maximum number of elements/characters.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class MaxLengthAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="MaxLengthAttribute"/>.</summary>
    /// <param name="length">The maximum allowed length.</param>
    public MaxLengthAttribute(int length)
        : base(ValidationCodes.MaximumLength, "{Property} must contain no more than {MaximumLength} characters.")
    {
        Length = length;
    }

    /// <summary>Gets the maximum allowed length.</summary>
    public int Length { get; }
}
