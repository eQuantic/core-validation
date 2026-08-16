; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
VALGEN001 | Validation | Warning | Generated registration requires eQuantic.Validation and Microsoft.Extensions.DependencyInjection.Abstractions
VALGEN002 | Validation | Error | CustomRule method returning bool was not found on the target type
VALGEN003 | Validation | Warning | ValidateEach element type could not be resolved from the property type
VALGEN004 | Validation | Warning | Validation rule or target is not supported by the generator
