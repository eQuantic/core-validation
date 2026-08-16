namespace eQuantic.Validation.Attributes;

/// <summary>Requires a string or collection to contain at least the specified minimum number of elements/characters.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class MinLengthAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="MinLengthAttribute"/>.</summary>
    /// <param name="length">The minimum allowed length.</param>
    public MinLengthAttribute(int length)
        : base(ValidationCodes.MinimumLength, "{Property} must contain at least {MinimumLength} characters.")
    {
        Length = length;
    }

    /// <summary>Gets the minimum allowed length.</summary>
    public int Length { get; }
}
