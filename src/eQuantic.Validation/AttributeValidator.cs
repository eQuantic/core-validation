using System.ComponentModel.DataAnnotations;

namespace eQuantic.Validation;

/// <summary>
/// Adapts standard <see cref="ValidationAttribute"/> annotations into the eQuantic result model.
/// Use this for DTO edges; fluent validators remain the preferred home for domain and asynchronous rules.
/// This adapter relies on runtime reflection and is not compatible with trimming or Native AOT;
/// prefer <c>[GenerateValidator]</c> for reflection-free attribute validation.
/// </summary>
/// <typeparam name="T">The annotated model type.</typeparam>
#if NET8_0_OR_GREATER
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
    "DataAnnotations validation discovers attributes through reflection over members that may be trimmed.")]
#endif
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

        Dictionary<object, object?>? items = null;
        if (context?.Items is { Count: > 0 } contextItems)
        {
            items = new Dictionary<object, object?>(contextItems.Count);
            foreach (var item in contextItems)
            {
                items[item.Key] = item.Value;
            }
        }

        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var attributeContext = new System.ComponentModel.DataAnnotations.ValidationContext(
            instance,
            context?.Services,
            items);

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
