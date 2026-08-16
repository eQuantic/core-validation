namespace eQuantic.Validation.Internal;

internal sealed class DelegatingRule<T> : IValidationRule<T>
{
    private readonly IValidator<T> _validator;

    public DelegatingRule(IValidator<T> validator) => _validator = validator;

    public bool IsAsync => true;

    public IEnumerable<ValidationRuleDescriptor> Describe() =>
        _validator is IDescribableValidator describable
            ? describable.Describe().Rules
            : Array.Empty<ValidationRuleDescriptor>();

    public bool AppliesTo(ValidationContext context) => true;

    public IReadOnlyList<ValidationFailure> Validate(T instance, ValidationContext context) => _validator.Validate(instance, context).Failures;

    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken) =>
        (await _validator.ValidateAsync(instance, context, cancellationToken).ConfigureAwait(false)).Failures;
}
