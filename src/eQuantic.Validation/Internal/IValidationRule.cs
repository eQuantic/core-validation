namespace eQuantic.Validation.Internal;

internal interface IValidationRule<T>
{
    bool IsAsync { get; }

    bool AppliesTo(ValidationContext context);

    IReadOnlyList<ValidationFailure> Validate(T instance, ValidationContext context);

    Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken);
}
