using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Generated;
using eQuantic.Validation.GeneratorFixtures;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class GeneratedRegistrationTests
{
    [Test]
    public async Task AddGeneratedValidation_registers_discovered_validators_without_manual_registration()
    {
        var services = new ServiceCollection();
        services.AddGeneratedValidation();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();

        var result = await dispatcher.ValidateAsync(new GeneratedRegistrationRequest(string.Empty), scope.ServiceProvider);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Code, Is.EqualTo("request.name.required"));
        });
    }

    [Test]
    public async Task Validators_from_referenced_assemblies_are_opt_in_via_AddValidator()
    {
        var services = new ServiceCollection();
        services.AddGeneratedValidation();

        // Referenced assemblies are never scanned or auto-registered; each one is composed
        // explicitly (or exposes its own registration method).
        services.AddValidator<ReferencedGeneratorRequest, ReferencedGeneratorValidator>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();

        var result = await dispatcher.ValidateAsync(new ReferencedGeneratorRequest("invalid"), scope.ServiceProvider);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Code, Is.EqualTo("referenced.email.invalid"));
        });
    }
}

internal sealed record GeneratedRegistrationRequest(string Name);

internal sealed class GeneratedRegistrationValidator : Validator<GeneratedRegistrationRequest>
{
    public GeneratedRegistrationValidator()
    {
        RuleFor(request => request.Name)
            .NotWhiteSpace()
            .WithCode("request.name.required");
    }
}
