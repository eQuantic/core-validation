namespace eQuantic.Validation;

/// <summary>The complete rule manifest of a validator.</summary>
public sealed class ValidatorDescription
{
    /// <summary>Initializes a validator description.</summary>
    public ValidatorDescription(Type modelType, IReadOnlyList<ValidationRuleDescriptor> rules)
    {
        ModelType = modelType ?? throw new ArgumentNullException(nameof(modelType));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>Gets the model type the rules apply to.</summary>
    public Type ModelType { get; }

    /// <summary>Gets every rule in declaration order.</summary>
    public IReadOnlyList<ValidationRuleDescriptor> Rules { get; }
}

/// <summary>
/// A validator that can describe its rules as data. Implemented by the fluent
/// <c>Validator&lt;T&gt;</c> base class and by source-generated validators.
/// </summary>
public interface IDescribableValidator
{
    /// <summary>Returns the rule manifest of this validator.</summary>
    ValidatorDescription Describe();
}
