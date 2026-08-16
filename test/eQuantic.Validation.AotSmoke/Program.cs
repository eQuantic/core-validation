// Native AOT smoke: published with PublishAot=true in CI and executed as a native binary.
// Exercises every advertised AOT-compatible path — source-generated validators, fluent
// validators with pre-compiled accessors, DI dispatching, manifests, the Zod exporter and
// built-in message translations. Any unexpected outcome exits non-zero and fails the build.
using System.Globalization;
using eQuantic.Validation;
using eQuantic.Validation.Attributes;
using eQuantic.Validation.Export;
using eQuantic.Validation.Generated;
using Microsoft.Extensions.DependencyInjection;

var checks = new List<(string Name, bool Passed)>();
void Check(string name, bool passed) => checks.Add((name, passed));

// --- 1. Source-generated validator (attributes, no reflection) ---
var generated = new CreateCustomerGeneratedValidator();
Check("generated: valid model passes",
    generated.Validate(new CreateCustomer("Ada Lovelace", "ada@example.com", 36)).IsValid);
var generatedFailures = generated.Validate(new CreateCustomer("", "not-an-email", 15));
Check("generated: invalid model fails with stable codes",
    !generatedFailures.IsValid &&
    generatedFailures.Errors.Any(f => f.Code == "customer.email.invalid"));

// --- 2. Fluent validator (pre-compiled accessors registered at module load) ---
var fluent = new BookingValidator();
Check("fluent: valid model passes",
    fluent.Validate(new Booking(new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), "a@x.com", "a@x.com",
        new[] { new Guest("Ada") })).IsValid);
var fluentFailures = fluent.Validate(new Booking(new DateTime(2026, 1, 5), new DateTime(2026, 1, 1), "a@x.com", "b@x.com",
    new[] { new Guest(" ") }));
Check("fluent: cross-property and child rules fail correctly",
    fluentFailures.Errors.Any(f => f.Path == "End") &&
    fluentFailures.Errors.Any(f => f.Path == "ConfirmEmail") &&
    fluentFailures.Errors.Any(f => f.Path == "Guests[0].Name"));

// --- 3. DI dispatching through generated registration ---
var services = new ServiceCollection();
services.AddGeneratedValidation();
await using (var provider = services.BuildServiceProvider())
await using (var scope = provider.CreateAsyncScope())
{
    var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();
    var dispatched = await dispatcher.ValidateAsync(
        new CreateCustomer("", "nope", 1), scope.ServiceProvider);
    Check("dispatcher: resolves and runs registered validators", !dispatched.IsValid);
}

// --- 4. Manifest + Zod export ---
var manifest = fluent.Describe();
Check("manifest: fluent rules described with paths and kinds",
    manifest.Rules.Any(r => r.Path == "Guests[].Name" && r.Kind == ValidationRuleKinds.NotWhiteSpace));
var zod = ZodSchemaExporter.Export(generated);
Check("zod: generated validator exports a schema",
    zod.Contains("email: z.string().email()") && zod.Contains("age: z.number().int().gte(18).lte(120)"));

// --- 5. Built-in translations under a non-English culture ---
var previousCulture = CultureInfo.CurrentUICulture;
try
{
    CultureInfo.CurrentUICulture = new CultureInfo("pt-BR");
    var localized = fluent.Validate(
        new Booking(new DateTime(2026, 1, 1), new DateTime(2026, 1, 5), null, null, Array.Empty<Guest>()),
        new ValidationContext(messageProvider: BuiltInValidationMessages.Instance));
    Check("i18n: built-in portuguese template resolved",
        localized.Errors.Any(f => f.Message == "Email não pode estar em branco."));
}
finally
{
    CultureInfo.CurrentUICulture = previousCulture;
}

// --- Report ---
var failed = checks.Where(c => !c.Passed).ToArray();
foreach (var (name, passed) in checks)
{
    Console.WriteLine($"{(passed ? "ok  " : "FAIL")} {name}");
}

if (failed.Length > 0)
{
    Console.Error.WriteLine($"{failed.Length} AOT smoke check(s) failed.");
    return 1;
}

Console.WriteLine($"AOT smoke passed: {checks.Count} checks.");
return 0;

[GenerateValidator]
public sealed record CreateCustomer(
    [Required, NotWhiteSpace] string Name,
    [Required, Email(Code = "customer.email.invalid")] string Email,
    [eQuantic.Validation.Attributes.Range(18, 120)] int Age);

public sealed record Booking(DateTime Start, DateTime End, string? Email, string? ConfirmEmail, IReadOnlyList<Guest> Guests);
public sealed record Guest(string? Name);

public sealed class BookingValidator : Validator<Booking>
{
    public BookingValidator()
    {
        RuleFor(x => x.End).GreaterThan(x => x.Start);
        RuleFor(x => x.Email).NotWhiteSpace().Email();
        RuleFor(x => x.ConfirmEmail).EqualTo(x => x.Email);
        RuleForEach(x => x.Guests).ChildRules(guest =>
            guest.RuleFor(g => g.Name).NotWhiteSpace());
    }
}
