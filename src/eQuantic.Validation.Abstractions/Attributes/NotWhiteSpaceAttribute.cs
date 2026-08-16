namespace eQuantic.Validation.Attributes;

/// <summary>Requires a non-empty, non-whitespace string.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class NotWhiteSpaceAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="NotWhiteSpaceAttribute"/>.</summary>
    public NotWhiteSpaceAttribute()
        : base(ValidationCodes.NotWhiteSpace, "{Property} must not be blank.")
    {
    }
}
