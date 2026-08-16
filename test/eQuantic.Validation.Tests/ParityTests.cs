using System.Globalization;
using eQuantic.Validation.Export;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class ParityTests
{
    [Test]
    public void Cross_property_comparisons_validate_against_other_members()
    {
        var validator = new InlineValidator<Booking>(v =>
        {
            v.RuleFor(x => x.End).GreaterThan(x => x.Start);
            v.RuleFor(x => x.ConfirmEmail).EqualTo(x => x.Email);
        });

        var valid = new Booking(Start: new DateTime(2026, 1, 1), End: new DateTime(2026, 1, 2), "a@x.com", "a@x.com");
        var swapped = valid with { End = new DateTime(2025, 12, 31) };
        var mismatch = valid with { ConfirmEmail = "b@x.com" };
        var confirmMissing = valid with { ConfirmEmail = null };

        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(valid).IsValid, Is.True);
            Assert.That(validator.Validate(swapped).Errors.Single().Path, Is.EqualTo("End"));
            Assert.That(validator.Validate(swapped).Errors.Single().Message,
                Is.EqualTo("End must be greater than Start."));
            Assert.That(validator.Validate(mismatch).Errors.Single().Path, Is.EqualTo("ConfirmEmail"));
            Assert.That(validator.Validate(confirmMissing).IsValid, Is.False,
                "EqualTo against another member is not null-permissive: confirmation fields must match");
        });
    }

    [Test]
    public void Cross_property_rules_carry_the_other_path_in_the_manifest_and_stay_out_of_zod_bounds()
    {
        var validator = new InlineValidator<Booking>(v => v.RuleFor(x => x.End).GreaterThan(x => x.Start));

        var rule = validator.Describe().Rules.Single();
        var schema = ZodSchemaExporter.Export(validator.Describe());

        Assert.Multiple(() =>
        {
            Assert.That(rule.Kind, Is.EqualTo(ValidationRuleKinds.GreaterThan));
            Assert.That(rule.Arguments["OtherPath"], Is.EqualTo("Start"));
            Assert.That(schema, Does.Not.Contain(".gt("), "a member comparison must not become a numeric bound");
            Assert.That(schema, Does.Contain("/* server-side: range */"), "cross-property rules surface as server-side notes");
        });
    }

    [Test]
    public void ChildRules_validate_nested_members_and_collection_elements_inline()
    {
        var validator = new InlineValidator<Company>(v =>
        {
            v.RuleFor(x => x.Headquarters!).ChildRules(hq =>
                hq.RuleFor(a => a.Street).NotWhiteSpace());
            v.RuleForEach(x => x.Branches).ChildRules(branch =>
                branch.RuleFor(a => a.Street).NotWhiteSpace());
        });

        var company = new Company(new Address(""), new[] { new Address("Main"), new Address(" ") });
        var result = validator.Validate(company);

        Assert.Multiple(() =>
        {
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Headquarters.Street"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Branches[1].Street"));
            Assert.That(validator.Describe().Rules.Select(rule => rule.Path), Does.Contain("Branches[].Street"));
        });
    }

    [Test]
    public void BuiltIn_messages_translate_default_templates_per_ui_culture()
    {
        var validator = new InlineValidator<Company>(v =>
        {
            v.RuleFor(x => x.Name).NotWhiteSpace();
            v.RuleFor(x => x.Name)
                .MinimumLength(3)
                .WithMessage("Custom too-short message.");
        });

        var context = new ValidationContext(messageProvider: BuiltInValidationMessages.Instance);
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
            var portuguese = validator.Validate(new Company(null, Array.Empty<Address>(), Name: "ab"), context);

            CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
            var unsupported = validator.Validate(new Company(null, Array.Empty<Address>(), Name: " "), context);

            Assert.Multiple(() =>
            {
                Assert.That(portuguese.Errors.Select(error => error.Message),
                    Does.Contain("Name deve conter pelo menos 3 caracteres.").Or.Contain("Custom too-short message."));
                Assert.That(portuguese.Errors.Single(error => error.Code == ValidationCodes.MinimumLength).Message,
                    Is.EqualTo("Custom too-short message."),
                    "messages customized with WithMessage are never translated");
                Assert.That(unsupported.Errors.Single(error => error.Code == ValidationCodes.NotWhiteSpace).Message,
                    Is.EqualTo("Name must not be blank."),
                    "unsupported cultures fall back to English");
            });
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Test]
    public void BuiltIn_messages_translate_the_blank_template_in_portuguese()
    {
        var validator = new InlineValidator<Company>(v => v.RuleFor(x => x.Name).NotWhiteSpace());
        var context = new ValidationContext(messageProvider: BuiltInValidationMessages.Instance);

        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
            var result = validator.Validate(new Company(null, Array.Empty<Address>(), Name: " "), context);

            Assert.That(result.Errors.Single().Message, Is.EqualTo("Name não pode estar em branco."));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private sealed record Booking(DateTime Start, DateTime End, string? Email, string? ConfirmEmail);
    private sealed record Address(string? Street);
    private sealed record Company(Address? Headquarters, IReadOnlyList<Address> Branches, string? Name = null);
}
