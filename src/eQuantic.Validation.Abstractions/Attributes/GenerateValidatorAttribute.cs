namespace eQuantic.Validation.Attributes;

/// <summary>
/// Instructs the source generator to generate a zero-allocation, Native AOT-compatible validator for this type at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class GenerateValidatorAttribute : Attribute
{
}
