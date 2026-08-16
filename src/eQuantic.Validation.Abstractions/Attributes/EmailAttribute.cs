namespace eQuantic.Validation.Attributes;

/// <summary>Requires the property to be a valid email address.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class EmailAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="EmailAttribute"/>.</summary>
    public EmailAttribute()
        : base(ValidationCodes.Email, "{Property} must be a valid email address.")
    {
    }
}
