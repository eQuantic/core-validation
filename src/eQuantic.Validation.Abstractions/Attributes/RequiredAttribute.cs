namespace eQuantic.Validation.Attributes;

/// <summary>Requires the property value to not be null.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class RequiredAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="RequiredAttribute"/>.</summary>
    public RequiredAttribute()
        : base(ValidationCodes.Required, "{Property} is required.")
    {
    }
}
