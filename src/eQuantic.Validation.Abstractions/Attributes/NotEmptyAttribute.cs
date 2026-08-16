namespace eQuantic.Validation.Attributes;

/// <summary>Requires the property value to not be empty (or default value).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class NotEmptyAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="NotEmptyAttribute"/>.</summary>
    public NotEmptyAttribute()
        : base(ValidationCodes.NotEmpty, "{Property} must not be empty.")
    {
    }
}
