using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace eQuantic.Validation.Generator.Tests;

/// <summary>
/// Runs the incremental generator against in-memory compilations and compiles its output for
/// real, covering the emission bugs that shipped in 1.0.0: culture-dependent numeric literals,
/// the Severity=Information rename, struct targets and reflection-based collection validation.
/// </summary>
[TestFixture]
public sealed class GeneratorEmissionTests
{
    private const string Preamble = """
        using System.Collections.Generic;
        using eQuantic.Validation;
        using eQuantic.Validation.Attributes;

        namespace Generated.Tests;

        """;

    [Test]
    public void Information_severity_compiles_and_uses_the_real_enum_member()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed record Model(
                [Required(Severity = ValidationSeverity.Information)] string Name);
            """);

        AssertCompiles(run);
        Assert.That(GeneratedText(run), Does.Contain("ValidationSeverity.Information"));
    }

    [Test]
    public void Decimal_threshold_compiles_under_comma_decimal_culture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");

            var run = RunGenerator(Preamble + """
                [GenerateValidator]
                public sealed record Payment(
                    [GreaterThan(0.5)] decimal Amount,
                    [Range(0.5, 99.5)] double Rate);
                """);

            AssertCompiles(run);
            var text = GeneratedText(run);
            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Contain("0.5m"), "decimal thresholds need an invariant literal with the 'm' suffix");
                Assert.That(text, Does.Contain("0.5d"), "double thresholds need an invariant literal with the 'd' suffix");
                Assert.That(text, Does.Not.Contain("0,5"), "the host culture must never leak into generated code");
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public void Struct_targets_compile_without_null_checks_on_the_instance()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public struct Coordinates
            {
                [Range(-90, 90)] public double Latitude { get; set; }
                [Range(-180, 180)] public double Longitude { get; set; }
            }
            """);

        AssertCompiles(run);
        Assert.That(GeneratedText(run), Does.Not.Contain("instance is null"));
    }

    [Test]
    public void Partial_declarations_generate_a_single_validator()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed partial record Split([Required] string Name);

            public sealed partial record Split
            {
                public string? Extra { get; init; }
            }
            """);

        AssertCompiles(run);
        var validatorTrees = run.Result.GeneratedTrees
            .Count(tree => tree.FilePath.Contains("Split", StringComparison.Ordinal));
        Assert.That(validatorTrees, Is.EqualTo(1));
    }

    [Test]
    public void ValidateEach_resolves_a_typed_validator_without_reflection()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed record Child([Required] string Name);

            [GenerateValidator]
            public sealed record Parent([ValidateEach] IReadOnlyList<Child> Children);
            """);

        AssertCompiles(run);
        var text = GeneratedText(run);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("IValidator<global::Generated.Tests.Child>"));
            Assert.That(text, Does.Not.Contain("MakeGenericType"));
        });
    }

    [Test]
    public void CustomRule_with_missing_method_reports_VALGEN002_and_skips_the_rule()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed record Item([CustomRule("DoesNotExist")] string Sku);
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.Diagnostics.Select(d => d.Id), Does.Contain("VALGEN002"));
            AssertCompiles(run);
        });
    }

    [Test]
    public void String_rule_on_non_string_property_reports_VALGEN004_and_still_compiles()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed record Weird([Email] int Age);
            """);

        Assert.Multiple(() =>
        {
            Assert.That(run.Diagnostics.Select(d => d.Id), Does.Contain("VALGEN004"));
            AssertCompiles(run);
        });
    }

    [Test]
    public void Generated_code_preserves_template_and_arguments_for_localization()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed record Doc([MinLength(3)] string Title);
            """);

        AssertCompiles(run);
        var text = GeneratedText(run);
        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("{Property} must contain at least {MinimumLength} characters."));
            Assert.That(text, Does.Contain("[\"MinimumLength\"] = 3"));
        });
    }

    [Test]
    public void Registration_extension_is_internal_and_targets_the_core_package()
    {
        var run = RunGenerator(Preamble + """
            [GenerateValidator]
            public sealed record Model([Required] string Name);
            """);

        AssertCompiles(run);
        var registrations = run.Result.GeneratedTrees
            .Single(tree => tree.FilePath.Contains("GeneratedRegistrations", StringComparison.Ordinal))
            .ToString();
        Assert.Multiple(() =>
        {
            Assert.That(registrations, Does.Contain("internal static class ValidationGeneratedExtensions"));
            Assert.That(registrations, Does.Contain("AddValidationDispatcher"));
        });
    }

    private sealed record GeneratorRun(
        Compilation Output,
        GeneratorDriverRunResult Result,
        ImmutableArray<Diagnostic> Diagnostics);

    private static GeneratorRun RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

        var referencePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
        {
            referencePaths.Add(path);
        }

        referencePaths.Add(typeof(Attributes.GenerateValidatorAttribute).Assembly.Location);
        referencePaths.Add(typeof(Validator<>).Assembly.Location);
        referencePaths.Add(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location);

        var compilation = CSharpCompilation.Create(
            "GeneratorEmissionTests",
            new[] { syntaxTree },
            referencePaths.Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ValidatorRegistrationGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return new GeneratorRun(outputCompilation, driver.GetRunResult(), diagnostics);
    }

    private static void AssertCompiles(GeneratorRun run)
    {
        var errors = run.Output.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(static diagnostic => diagnostic.ToString())
            .ToArray();

        Assert.That(errors, Is.Empty, "generated code must compile:\n" + string.Join("\n", errors));
    }

    private static string GeneratedText(GeneratorRun run) =>
        string.Join("\n\n", run.Result.GeneratedTrees.Select(static tree => tree.ToString()));
}
