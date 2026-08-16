namespace eQuantic.Validation.Attributes;

/// <summary>Requires a string property value to match a regular expression pattern.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, Inherited = true, AllowMultiple = false)]
public sealed class PatternAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="PatternAttribute"/>.</summary>
    /// <param name="regex">The regular expression pattern.</param>
    public PatternAttribute(string regex)
        : base(ValidationCodes.Pattern, "{Property} has an invalid format.")
    {
        Regex = regex ?? throw new ArgumentNullException(nameof(regex));
    }

    /// <summary>Gets the regular expression pattern.</summary>
    public string Regex { get; }
}
