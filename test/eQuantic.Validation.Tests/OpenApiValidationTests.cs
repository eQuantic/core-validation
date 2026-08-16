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
}

[GenerateValidator]
public sealed record OpenApiSampleCustomer(
    [Required] string Name,
    [Required, Email] string Email,
    [eQuantic.Validation.Attributes.Range(18, 100)] int Age
);
#endif
