namespace eQuantic.Validation;

/// <summary>Indicates the business importance of a failed validation rule.</summary>
public enum ValidationSeverity
{
    /// <summary>The input is invalid and must not proceed.</summary>
    Error = 0,

    /// <summary>The input may proceed but the caller should surface the issue.</summary>
    Warning = 1,

    /// <summary>The input may proceed and the issue is informational.</summary>
    Information = 2,
}
