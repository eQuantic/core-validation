using eQuantic.Validation.Export;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class ZodExportTests
{
    [Test]
    public void Export_maps_structural_rules_to_zod_constraints()
    {
        var schema = ZodSchemaExporter.Export(new CheckoutValidator());

        Assert.Multiple(() =>
        {
            Assert.That(schema, Does.Contain("import { z } from \"zod\";"));
            Assert.That(schema, Does.Contain("export const checkoutSchema = z.object({"));
            Assert.That(schema, Does.Contain("customerName: z.string().min(1).min(2).max(60),"));
            Assert.That(schema, Does.Contain("email: z.string().min(1).email()"));
            Assert.That(schema, Does.Contain("couponCode: z.string().regex(/^[A-Z0-9]{6}$/).optional(),"));
            Assert.That(schema, Does.Contain("amount: z.number().gt(0),"));
            Assert.That(schema, Does.Contain("installments: z.number().int().gte(1).lte(12),"));
            Assert.That(schema, Does.Contain("export type Checkout = z.infer<typeof checkoutSchema>;"));
        });
    }

    [Test]
    public void Export_builds_nested_objects_and_arrays_from_paths()
    {
        var schema = ZodSchemaExporter.Export(new CheckoutValidator());

        Assert.Multiple(() =>
        {
            Assert.That(schema, Does.Contain("address: z.object({"));
            Assert.That(schema, Does.Contain("street: z.string().min(1),"));
            Assert.That(schema, Does.Contain("items: z.array(z.object({"));
            Assert.That(schema, Does.Contain("sku: z.string().min(1),"));
        });
    }

    [Test]
    public void Export_surfaces_opaque_rules_as_server_side_comments()
    {
        var schema = ZodSchemaExporter.Export(new CheckoutValidator());

        Assert.That(schema, Does.Contain("/* server-side: checkout.email.taken */"));
    }

    [Test]
    public void Export_excludes_scenario_rules_unless_requested()
    {
        var defaultSchema = ZodSchemaExporter.Export(new CheckoutValidator());
        var adminSchema = ZodSchemaExporter.Export(
            new CheckoutValidator(),
            new ZodExportOptions { Scenario = "admin" });

        Assert.Multiple(() =>
        {
            Assert.That(defaultSchema, Does.Not.Contain("internalNote"));
            Assert.That(adminSchema, Does.Contain("internalNote: z.string().min(1),"));
        });
    }

    [Test]
    public void Export_works_from_generated_validators_too()
    {
        var schema = ZodSchemaExporter.Export(new SourceGenUserGeneratedValidator());

        Assert.Multiple(() =>
        {
            Assert.That(schema, Does.Contain("name: z.string().min(1),"));
            Assert.That(schema, Does.Contain("email: z.string().email(),"));
            Assert.That(schema, Does.Contain("age: z.number().int().gte(18).lte(120),"));
        });
    }

    private sealed record Checkout(
        string? CustomerName,
        string? Email,
        string? CouponCode,
        decimal Amount,
        int Installments,
        string? InternalNote,
        Address? Address,
        IReadOnlyList<Item> Items);

    private sealed record Address(string? Street);
    private sealed record Item(string? Sku);

    private sealed class CheckoutValidator : Validator<Checkout>
    {
        public CheckoutValidator()
        {
            RuleFor(x => x.CustomerName).NotWhiteSpace().Length(2, 60);
            RuleFor(x => x.Email).NotWhiteSpace().Email();
            RuleFor(x => x.Email)
                .MustAsync(static (_, _, _, _) => Task.FromResult(true), "checkout.email.taken");
            RuleFor(x => x.CouponCode).Matches("^[A-Z0-9]{6}$");
            RuleFor(x => x.Amount).GreaterThan(0);
            RuleFor(x => x.Installments).InclusiveBetween(1, 12);
            RuleFor(x => x.InternalNote).NotWhiteSpace().ForScenarios("admin");
            RuleFor(x => x.Address!).SetValidator(new AddressValidator());
            RuleForEach(x => x.Items).SetValidator(new ItemValidator());
        }
    }

    private sealed class AddressValidator : Validator<Address>
    {
        public AddressValidator()
        {
            RuleFor(x => x.Street).NotWhiteSpace();
        }
    }

    private sealed class ItemValidator : Validator<Item>
    {
        public ItemValidator()
        {
            RuleFor(x => x.Sku).NotWhiteSpace();
        }
    }
}
