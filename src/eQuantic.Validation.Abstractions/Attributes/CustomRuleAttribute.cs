namespace eQuantic.Validation.Attributes;

/// <summary>
/// Invokes a custom synchronous or asynchronous validation method on the model or a static method.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Class | AttributeTargets.Struct, Inherited = true, AllowMultiple = true)]
public sealed class CustomRuleAttribute : ValidationRuleAttribute
{
    /// <summary>Initializes a new instance of <see cref="CustomRuleAttribute"/> targeting an instance method.</summary>
    /// <param name="methodName">The validation method name.</param>
    public CustomRuleAttribute(string methodName)
        : base(ValidationCodes.Predicate, "{Property} is invalid.")
    {
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
    }

    /// <summary>Initializes a new instance of <see cref="CustomRuleAttribute"/> targeting a static method on a declaring type.</summary>
    /// <param name="declaringType">The type declaring the validation method.</param>
    /// <param name="methodName">The validation method name.</param>
    public CustomRuleAttribute(Type declaringType, string methodName)
        : base(ValidationCodes.Predicate, "{Property} is invalid.")
    {
        DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
    }

    /// <summary>Gets the optional type that declares the static validation method.</summary>
    public Type? DeclaringType { get; }

    /// <summary>Gets the name of the validation method.</summary>
    public string MethodName { get; }
}
