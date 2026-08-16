namespace eQuantic.Validation.Testing;

/// <summary>Entry points for testing validators with assertion-friendly results.</summary>
public static class ValidationTestExtensions
{
    /// <summary>Validates synchronously and wraps the outcome for assertions.</summary>
    public static TestValidationResult<T> TestValidate<T>(this IValidator<T> validator, T instance)
    {
        if (validator is null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        return new TestValidationResult<T>(validator.Validate(instance));
    }

    /// <summary>Validates synchronously under an explicit context (scenarios, paths, services).</summary>
    public static TestValidationResult<T> TestValidate<T>(
        this IValidator<T> validator,
        T instance,
        ValidationContext context)
    {
        if (validator is null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        return new TestValidationResult<T>(validator.Validate(instance, context));
    }

    /// <summary>Validates synchronously under the given scenarios.</summary>
    public static TestValidationResult<T> TestValidate<T>(
        this IValidator<T> validator,
        T instance,
        params string[] scenarios)
        => validator.TestValidate(instance, ValidationContext.ForScenarios(scenarios));

    /// <summary>Validates asynchronously (required when the validator has async rules) and wraps the outcome.</summary>
    public static async Task<TestValidationResult<T>> TestValidateAsync<T>(
        this IValidator<T> validator,
        T instance,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        return new TestValidationResult<T>(
            await validator.ValidateAsync(instance, cancellationToken: cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Validates asynchronously under an explicit context (scenarios, paths, services).</summary>
    public static async Task<TestValidationResult<T>> TestValidateAsync<T>(
        this IValidator<T> validator,
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (validator is null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        return new TestValidationResult<T>(
            await validator.ValidateAsync(instance, context, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Validates asynchronously under the given scenarios.</summary>
    public static Task<TestValidationResult<T>> TestValidateAsync<T>(
        this IValidator<T> validator,
        T instance,
        params string[] scenarios)
        => validator.TestValidateAsync(instance, ValidationContext.ForScenarios(scenarios));
}
