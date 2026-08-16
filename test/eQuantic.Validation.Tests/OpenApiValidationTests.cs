#if NET10_0_OR_GREATER
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Attributes;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class OpenApiValidationTests
{
    [Test]
    public void AddValidationTransformer_registers_without_errors()
    {
        var options = new OpenApiOptions();
        Assert.That(() => options.AddValidationTransformer(), Throws.Nothing);
    }

    [Test]
    public void EnrichSchema_populates_openapi_metadata_from_attributes()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["Name"] = new OpenApiSchema(),
                ["Email"] = new OpenApiSchema(),
                ["Age"] = new OpenApiSchema()
            }
        };

        schema.EnrichSchema(typeof(OpenApiSampleCustomer));

        Assert.Multiple(() =>
        {
            Assert.That(schema.Required, Does.Contain("Name"));
            Assert.That(schema.Required, Does.Contain("Email"));

            var emailSchema = schema.Properties["Email"] as OpenApiSchema;
            Assert.That(emailSchema?.Format, Is.EqualTo("email"));

            var ageSchema = schema.Properties["Age"] as OpenApiSchema;
            Assert.That(ageSchema?.Minimum, Is.EqualTo("18"));
            Assert.That(ageSchema?.Maximum, Is.EqualTo("100"));
        });
    }

    [Test]
    public void EnrichSchema_applies_fluent_validator_manifests()
    {
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema(),
                ["postalCode"] = new OpenApiSchema(),
                ["amount"] = new OpenApiSchema(),
                ["email"] = new OpenApiSchema()
            }
        };

        schema.EnrichSchema(new OpenApiFluentValidator().Describe());

        Assert.Multiple(() =>
        {
            // required maps from NotWhiteSpace; property matching is case-insensitive (camelCase schema)
            Assert.That(schema.Required, Does.Contain("name"));

            var nameSchema = schema.Properties["name"] as OpenApiSchema;
            Assert.That(nameSchema?.MinLength, Is.EqualTo(2));
            Assert.That(nameSchema?.MaxLength, Is.EqualTo(50));

            var postalSchema = schema.Properties["postalCode"] as OpenApiSchema;
            Assert.That(postalSchema?.Pattern, Is.EqualTo("^[0-9]{5}$"));

            var amountSchema = schema.Properties["amount"] as OpenApiSchema;
            Assert.That(amountSchema?.ExclusiveMinimum, Is.EqualTo("0"));

            var emailSchema = schema.Properties["email"] as OpenApiSchema;
            Assert.That(emailSchema?.Format, Is.EqualTo("email"));

            // scenario-scoped rules must not leak into the schema
            Assert.That(schema.Required, Does.Not.Contain("email"));
        });
    }

    private sealed record OpenApiFluentModel(string? Name, string? PostalCode, decimal Amount, string? Email);

    private sealed class OpenApiFluentValidator : Validator<OpenApiFluentModel>
    {
        public OpenApiFluentValidator()
        {
            RuleFor(x => x.Name).NotWhiteSpace().MinimumLength(2).MaximumLength(50);
            RuleFor(x => x.PostalCode).Matches("^[0-9]{5}$");
            RuleFor(x => x.Amount).GreaterThan(0);
            RuleFor(x => x.Email).Email();
            RuleFor(x => x.Email).NotWhiteSpace().ForScenarios("create");
        }
    }
}

[GenerateValidator]
public sealed record OpenApiSampleCustomer(
    [Required] string Name,
    [Required, Email] string Email,
    [eQuantic.Validation.Attributes.Range(18, 100)] int Age
);
#endif
