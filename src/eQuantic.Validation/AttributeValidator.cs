using System.ComponentModel.DataAnnotations;

namespace eQuantic.Validation;

/// <summary>
/// Adapts standard <see cref="ValidationAttribute"/> annotations into the eQuantic result model.
/// Use this for DTO edges; fluent validators remain the preferred home for domain and asynchronous rules.
/// </summary>
/// <typeparam name="T">The annotated model type.</typeparam>
public sealed class AttributeValidator<T> : IValidator<T>
{
    /// <inheritdoc />
    public Type ValidatedType => typeof(T);

    /// <inheritdoc />
    public ValidationResult Validate(T instance, ValidationContext? context = null)
    {
        if (instance is null)
        {
            return ValidationResult.Failure(new ValidationFailure(string.Empty, ValidationCodes.Required, "The request body is required."));
        }

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var attributeContext = new System.ComponentModel.DataAnnotations.ValidationContext(
            instance,
            context?.Services,
            context?.Items is null ? null : new Dictionary<object, object?>());

        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            instance,
            attributeContext,
            results,
            validateAllProperties: true);

        var failures = results.SelectMany(result =>
        {
            var members = result.MemberNames.DefaultIfEmpty(string.Empty);
            return members.Select(member => new ValidationFailure(
                member,
                ValidationCodes.Attribute,
                result.ErrorMessage ?? "The value is invalid."));
        });

        return new ValidationResult(failures);
    }

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(
        T instance,
        ValidationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Validate(instance, context));
    }

    ValidationResult IValidator.Validate(object instance, ValidationContext? context) => Validate(Cast(instance), context);

    Task<ValidationResult> IValidator.ValidateAsync(
        object instance,
        ValidationContext? context,
        CancellationToken cancellationToken) => ValidateAsync(Cast(instance), context, cancellationToken);

    private static T Cast(object instance)
    {
        if (instance is T typed)
        {
            return typed;
        }

        throw new ArgumentException($"Expected an instance of '{typeof(T).FullName}'.", nameof(instance));
    }
}
