namespace eQuantic.Validation;

/// <summary>Thrown when synchronous validation is requested for a validator that contains active asynchronous rules.</summary>
public sealed class AsyncValidationRequiredException : InvalidOperationException
{
    /// <summary>Initializes the exception.</summary>
    public AsyncValidationRequiredException(Type validatedType)
        : base($"The validator for '{validatedType.FullName}' contains asynchronous rules. Use ValidateAsync instead.")
    {
    }
}
