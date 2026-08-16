using Microsoft.CodeAnalysis.Text;

namespace eQuantic.Validation.Generator;

internal enum RuleKind
{
    Required,
    NotEmpty,
    NotWhiteSpace,
    Email,
    MinLength,
    MaxLength,
    Length,
    Range,
    Pattern,
    GreaterThan,
    LessThan,
    ValidateNested,
    ValidateEach,
    CustomRule,
}

/// <summary>A rule argument with its value already rendered as a C# literal (culture-safe).</summary>
internal sealed record RuleArgument(string Name, string CSharpLiteral);

internal sealed record RuleDescriptor(
    RuleKind Kind,
    string Code,
    string MessageTemplate,
    int Severity,
    EquatableArray<string> Scenarios,
    EquatableArray<RuleArgument> Arguments,
    string? Pattern = null,
    string? ElementTypeName = null,
    string? CustomMethodName = null,
    string? CustomDeclaringType = null);

internal sealed record PropertyDescriptor(
    string PropertyName,
    string TypeName,
    string BareTypeName,
    bool IsNullableOrReference,
    bool IsString,
    EquatableArray<RuleDescriptor> Rules);

internal sealed record DiagnosticInfo(
    string Id,
    string Message,
    string FilePath,
    int SpanStart,
    int SpanLength,
    int StartLine,
    int StartCharacter,
    int EndLine,
    int EndCharacter)
{
    public Microsoft.CodeAnalysis.Location ToLocation()
    {
        if (string.IsNullOrEmpty(FilePath))
        {
            return Microsoft.CodeAnalysis.Location.None;
        }

        return Microsoft.CodeAnalysis.Location.Create(
            FilePath,
            new TextSpan(SpanStart, SpanLength),
            new LinePositionSpan(
                new LinePosition(StartLine, StartCharacter),
                new LinePosition(EndLine, EndCharacter)));
    }

    public static DiagnosticInfo Create(string id, string message, Microsoft.CodeAnalysis.Location? location)
    {
        if (location is null || location.SourceTree is null)
        {
            return new DiagnosticInfo(id, message, string.Empty, 0, 0, 0, 0, 0, 0);
        }

        var lineSpan = location.GetLineSpan().Span;
        return new DiagnosticInfo(
            id,
            message,
            location.SourceTree.FilePath,
            location.SourceSpan.Start,
            location.SourceSpan.Length,
            lineSpan.Start.Line,
            lineSpan.Start.Character,
            lineSpan.End.Line,
            lineSpan.End.Character);
    }
}

internal sealed record ModelDescriptor(
    string ModelName,
    string Namespace,
    string FullModelTypeName,
    bool IsReferenceType,
    string GeneratedValidatorName,
    string FullGeneratedValidatorTypeName,
    string HintName,
    EquatableArray<PropertyDescriptor> Properties,
    EquatableArray<DiagnosticInfo> Diagnostics);

internal sealed record ValidatorDescriptor(string ValidatorType, string ModelType);

internal sealed record RegistrationCapabilities(bool HasCorePackage, bool HasServiceCollection);
