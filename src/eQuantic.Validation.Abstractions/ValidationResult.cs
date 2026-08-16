namespace eQuantic.Validation;

/// <summary>The complete outcome of a validation operation.</summary>
public sealed class ValidationResult
{
    private static readonly ValidationResult ValidResult = new(Array.Empty<ValidationFailure>());

    /// <summary>Initializes a result from validation failures.</summary>
    public ValidationResult(IEnumerable<ValidationFailure> failures)
    {
        Failures = Array.AsReadOnly(failures?.ToArray() ?? throw new ArgumentNullException(nameof(failures)));
    }

    /// <summary>Gets a successful validation result.</summary>
    public static ValidationResult Success => ValidResult;

    /// <summary>Gets all errors, warnings and informational issues in declaration order.</summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>Gets failures that prevent the input from proceeding.</summary>
    public IReadOnlyList<ValidationFailure> Errors =>
        Failures.Where(static failure => failure.Severity == ValidationSeverity.Error).ToArray();

    /// <summary>Gets non-blocking warnings.</summary>
    public IReadOnlyList<ValidationFailure> Warnings =>
        Failures.Where(static failure => failure.Severity == ValidationSeverity.Warning).ToArray();

    /// <summary>Gets whether the result has no blocking errors.</summary>
    public bool IsValid => !Failures.Any(static failure => failure.Severity == ValidationSeverity.Error);

    /// <summary>Creates a result containing one failure.</summary>
    public static ValidationResult Failure(ValidationFailure failure) => new(new[] { failure });

    /// <summary>Returns failures grouped by model-binding-compatible member path.</summary>
    public IDictionary<string, string[]> ToErrorDictionary()
    {
        return Errors
            .GroupBy(static failure => failure.Path)
            .ToDictionary(
                static group => group.Key,
                static group => group.Select(static failure => failure.Message).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Combines several results while preserving their order.</summary>
    public static ValidationResult Combine(IEnumerable<ValidationResult> results)
    {
        if (results is null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        return new ValidationResult(results.SelectMany(static result => result.Failures));
    }
}
