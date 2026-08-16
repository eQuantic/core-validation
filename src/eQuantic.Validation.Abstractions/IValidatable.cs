namespace eQuantic.Validation;

/// <summary>Defines a model that exposes self-validation capabilities.</summary>
/// <typeparam name="T">The model type.</typeparam>
public interface IValidatable<in T>
{
    /// <summary>Validates this instance using synchronous rules.</summary>
    ValidationResult Validate(ValidationContext? context = null);

    /// <summary>Validates this instance, including asynchronous rules.</summary>
    Task<ValidationResult> ValidateAsync(
        ValidationContext? context = null,
        CancellationToken cancellationToken = default);
}
