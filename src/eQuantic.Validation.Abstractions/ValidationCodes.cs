namespace eQuantic.Validation;

/// <summary>Built-in machine-readable validation codes.</summary>
public static class ValidationCodes
{
    /// <summary>A value is required.</summary>
    public const string Required = "required";
    /// <summary>A value must not be empty.</summary>
    public const string NotEmpty = "not_empty";
    /// <summary>A value must not contain only whitespace.</summary>
    public const string NotWhiteSpace = "not_whitespace";
    /// <summary>A value is not a valid email address.</summary>
    public const string Email = "email";
    /// <summary>A value is shorter than the allowed minimum.</summary>
    public const string MinimumLength = "minimum_length";
    /// <summary>A value is longer than the allowed maximum.</summary>
    public const string MaximumLength = "maximum_length";
    /// <summary>A value does not match a regular expression.</summary>
    public const string Pattern = "pattern";
    /// <summary>A value is outside an allowed range.</summary>
    public const string Range = "range";
    /// <summary>A value does not satisfy a custom predicate.</summary>
    public const string Predicate = "predicate";
    /// <summary>A DataAnnotations attribute rejected a value.</summary>
    public const string Attribute = "attribute";
}
