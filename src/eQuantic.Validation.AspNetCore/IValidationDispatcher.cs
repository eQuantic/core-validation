namespace eQuantic.Validation.AspNetCore;

/// <summary>Runs all eQuantic validators registered for a runtime model type.</summary>
public interface IValidationDispatcher
{
    /// <summary>Validates a model using validators resolved from the supplied request scope.</summary>
    Task<ValidationResult> ValidateAsync(
        object model,
        IServiceProvider services,
        ValidationContext? context = null,
        CancellationToken cancellationToken = default);
}
