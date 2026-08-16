namespace eQuantic.Validation;

/// <summary>Validates instances whose runtime type is known only at execution time.</summary>
public interface IValidator
{
    /// <summary>Gets the type accepted by this validator.</summary>
    Type ValidatedType { get; }

    /// <summary>Validates an instance using synchronous rules only.</summary>
    ValidationResult Validate(object instance, ValidationContext? context = null);

    /// <summary>Validates an instance, including asynchronous rules.</summary>
    Task<ValidationResult> ValidateAsync(
        object instance,
        ValidationContext? context = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Validates a strongly typed instance.</summary>
/// <typeparam name="T">The type being validated.</typeparam>
public interface IValidator<in T> : IValidator
{
    /// <summary>Validates an instance using synchronous rules only.</summary>
    ValidationResult Validate(T instance, ValidationContext? context = null);

    /// <summary>Validates an instance, including asynchronous rules.</summary>
    Task<ValidationResult> ValidateAsync(
        T instance,
        ValidationContext? context = null,
        CancellationToken cancellationToken = default);
}
