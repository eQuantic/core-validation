using eQuantic.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class ValidationWarningsTests
{
    [Test]
    public void FormatHeader_renders_compact_path_code_pairs()
    {
        var header = ValidationWarnings.FormatHeader(new[]
        {
            new ValidationFailure("Name", "deprecated.value", "Legacy name.", ValidationSeverity.Warning),
            new ValidationFailure("Address.City", "city.renamed", "City was renamed.", ValidationSeverity.Warning),
            new ValidationFailure(string.Empty, "model.legacy", "Legacy payload.", ValidationSeverity.Warning),
        });

        Assert.That(header, Is.EqualTo("Name:deprecated.value, Address.City:city.renamed, model.legacy"));
    }

    [Test]
    public async Task Valid_results_with_warnings_surface_header_and_items()
    {
        var validator = new InlineValidator<Profile>(v =>
            v.RuleFor(x => x.Nickname)
                .Must(static nickname => nickname != "legacy", "profile.nickname.legacy")
                .WithSeverity(ValidationSeverity.Warning));

        var result = await validator.ValidateAsync(new Profile("legacy"));
        var httpContext = new DefaultHttpContext();

        // Simulates what the HTTP filters do for a valid-but-warned request.
        Assert.That(result.IsValid, Is.True);
        typeof(ValidationWarnings)
            .GetMethod("Attach", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { httpContext, result.Warnings.ToList() });

        Assert.Multiple(() =>
        {
            Assert.That(httpContext.Response.Headers[ValidationWarnings.HeaderName].ToString(),
                Is.EqualTo("Nickname:profile.nickname.legacy"));
            Assert.That(ValidationWarnings.GetWarnings(httpContext)!.Single().Code,
                Is.EqualTo("profile.nickname.legacy"));
        });
    }

    [Test]
    public void No_warnings_means_no_header_and_no_items()
    {
        var httpContext = new DefaultHttpContext();

        typeof(ValidationWarnings)
            .GetMethod("Attach", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { httpContext, new List<ValidationFailure>() });

        Assert.Multiple(() =>
        {
            Assert.That(httpContext.Response.Headers.ContainsKey(ValidationWarnings.HeaderName), Is.False);
            Assert.That(ValidationWarnings.GetWarnings(httpContext), Is.Null);
        });
    }

    private sealed record Profile(string? Nickname);
}
