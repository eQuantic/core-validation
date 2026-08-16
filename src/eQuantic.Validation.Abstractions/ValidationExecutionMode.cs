namespace eQuantic.Validation;

/// <summary>Defines how independent top-level rules are scheduled.</summary>
public enum ValidationExecutionMode
{
    /// <summary>Executes rules in declaration order.</summary>
    Sequential = 0,

    /// <summary>Executes independent top-level rules concurrently while preserving result order.</summary>
    Parallel = 1,
}
