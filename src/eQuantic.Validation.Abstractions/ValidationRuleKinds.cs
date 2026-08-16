namespace eQuantic.Validation;

/// <summary>
/// Stable rule kinds used by <see cref="ValidationRuleDescriptor.Kind"/>. Kinds identify what a
/// rule checks independently of its error code, which callers may override per rule.
/// </summary>
public static class ValidationRuleKinds
{
    /// <summary>A value is required (non-null / non-default).</summary>
    public const string Required = "required";
    /// <summary>A value must not be empty.</summary>
    public const string NotEmpty = "not_empty";
    /// <summary>A string must not be blank.</summary>
    public const string NotWhiteSpace = "not_whitespace";
    /// <summary>A string must look like an email address.</summary>
    public const string Email = "email";
    /// <summary>A string must contain at least N characters.</summary>
    public const string MinimumLength = "minimum_length";
    /// <summary>A string must contain no more than N characters.</summary>
    public const string MaximumLength = "maximum_length";
    /// <summary>A string length must be inside an inclusive range.</summary>
    public const string Length = "length";
    /// <summary>A string must match a regular expression.</summary>
    public const string Pattern = "pattern";
    /// <summary>A value must equal an expected value.</summary>
    public const string Equal = "equal";
    /// <summary>A value must be strictly greater than a threshold.</summary>
    public const string GreaterThan = "greater_than";
    /// <summary>A value must be greater than or equal to a threshold.</summary>
    public const string GreaterThanOrEqual = "greater_than_or_equal";
    /// <summary>A value must be strictly less than a threshold.</summary>
    public const string LessThan = "less_than";
    /// <summary>A value must be less than or equal to a threshold.</summary>
    public const string LessThanOrEqual = "less_than_or_equal";
    /// <summary>A value must differ from a forbidden value.</summary>
    public const string NotEqual = "not_equal";
    /// <summary>A value must be one of an allowed set.</summary>
    public const string OneOf = "one_of";
    /// <summary>A value must be inside an inclusive range.</summary>
    public const string InclusiveBetween = "inclusive_between";
    /// <summary>A custom synchronous or asynchronous predicate.</summary>
    public const string Predicate = "predicate";
    /// <summary>A cross-field pattern-matching condition.</summary>
    public const string Match = "match";
    /// <summary>A value must instantiate a Value Object via factory.</summary>
    public const string Create = "create";
    /// <summary>A string must parse into a target type.</summary>
    public const string Parse = "parse";
    /// <summary>A nested member is validated by another validator.</summary>
    public const string Nested = "nested";
    /// <summary>Collection elements are validated by another validator.</summary>
    public const string Collection = "collection";
    /// <summary>A DataAnnotations attribute adapted at runtime.</summary>
    public const string Attribute = "attribute";
}
