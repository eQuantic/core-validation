using System.ComponentModel.DataAnnotations;
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Attributes;
using eQuantic.Validation.Generated;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class SourceGeneratedValidatorTests
{
    [Test]
    public void GeneratedValidator_validates_required_and_email_rules()
    {
        var validator = new SourceGenUserGeneratedValidator();
        var invalidUser = new SourceGenUser("", "not-an-email", 15);

        var result = validator.Validate(invalidUser);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Name"));
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Email"));
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Age"));
            Assert.That(result.Errors.Single(e => e.Path == "Email").Code, Is.EqualTo("custom.email.invalid"));
        });
    }

    [Test]
    public void GeneratedValidator_succeeds_when_all_rules_pass()
    {
        var validator = new SourceGenUserGeneratedValidator();
        var validUser = new SourceGenUser("John Doe", "john@example.com", 25);

        var result = validator.Validate(validUser);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void GeneratedValidator_honors_scenarios()
    {
        var validator = new SourceGenAccountGeneratedValidator();
        var account = new SourceGenAccount("12345", "STANDARD");

        var defaultResult = validator.Validate(account);
        var adminResult = validator.Validate(account, ValidationContext.ForScenarios("admin"));

        Assert.Multiple(() =>
        {
            Assert.That(defaultResult.IsValid, Is.True);
            Assert.That(adminResult.IsValid, Is.False);
            Assert.That(adminResult.Errors.Single().Code, Is.EqualTo("admin.tier.invalid"));
        });
    }

    [Test]
    public async Task GeneratedValidator_validates_nested_models_via_di()
    {
        var services = new ServiceCollection();
        services.AddGeneratedValidation();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();

        var invalidOrder = new SourceGenOrder("ORD-1", new SourceGenAddress(""));
        var result = await dispatcher.ValidateAsync(invalidOrder, scope.ServiceProvider);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Address.Street"));
        });
    }

    [Test]
    public async Task GeneratedValidator_validates_collections_via_di()
    {
        var services = new ServiceCollection();
        services.AddGeneratedValidation();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();

        var team = new SourceGenTeam("Devs", new[] { new SourceGenAddress("") });
        var result = await dispatcher.ValidateAsync(team, scope.ServiceProvider);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Offices[0].Street"));
        });
    }

    [Test]
    public void GeneratedValidator_executes_custom_rule_method()
    {
        var validator = new SourceGenItemGeneratedValidator();
        var item = new SourceGenItem("CODE-999");

        var result = validator.Validate(item);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single().Code, Is.EqualTo("item.sku.invalid"));
        });
    }

    [Test]
    public void GeneratedValidator_works_with_data_annotations()
    {
        var validator = new SourceGenAnnotatedGeneratedValidator();
        var invalid = new SourceGenAnnotated("", "too-long-value-for-code");

        var result = validator.Validate(invalid);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Title"));
            Assert.That(result.Errors.Select(e => e.Path), Does.Contain("Code"));
        });
    }
}

[GenerateValidator]
public sealed record SourceGenUser(
    [eQuantic.Validation.Attributes.Required, eQuantic.Validation.Attributes.NotWhiteSpace] string Name,
    [eQuantic.Validation.Attributes.Required, eQuantic.Validation.Attributes.Email(Code = "custom.email.invalid")] string Email,
    [eQuantic.Validation.Attributes.Range(18, 120)] int Age
);

[GenerateValidator]
public sealed record SourceGenAccount(
    [eQuantic.Validation.Attributes.Required] string AccountNumber,
    [eQuantic.Validation.Attributes.Pattern("^VIP-", Scenarios = ["admin"], Code = "admin.tier.invalid")] string Tier
);

[GenerateValidator]
public sealed record SourceGenAddress(
    [eQuantic.Validation.Attributes.Required, eQuantic.Validation.Attributes.NotWhiteSpace] string Street
);

[GenerateValidator]
public sealed record SourceGenOrder(
    [eQuantic.Validation.Attributes.Required] string OrderNumber,
    [ValidateNested] SourceGenAddress Address
);

[GenerateValidator]
public sealed record SourceGenTeam(
    [eQuantic.Validation.Attributes.Required] string Name,
    [ValidateEach] IReadOnlyList<SourceGenAddress> Offices
);

[GenerateValidator]
public sealed record SourceGenItem(
    [CustomRule("ValidateSku", Code = "item.sku.invalid", Message = "SKU must start with ITEM-")]
    string Sku
)
{
    public bool ValidateSku(string? sku) => sku is not null && sku.StartsWith("ITEM-");
}

[GenerateValidator]
public sealed record SourceGenAnnotated(
    [System.ComponentModel.DataAnnotations.Required] string Title,
    [System.ComponentModel.DataAnnotations.StringLength(5)] string Code
);
