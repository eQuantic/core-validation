using eQuantic.Validation.Export;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class ShortcutTests
{
    [Test]
    public void Comparison_shortcuts_validate_inclusive_and_forbidden_values()
    {
        var validator = new InlineValidator<Order>(v =>
        {
            v.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
            v.RuleFor(x => x.Discount).LessThanOrEqualTo(50);
            v.RuleFor(x => x.Status).NotEqualTo("deleted");
            v.RuleFor(x => x.Currency).OneOf("BRL", "EUR", "USD");
        });

        Assert.Multiple(() =>
        {
            Assert.That(validator.Validate(new Order(1, 50, "open", "BRL")).IsValid, Is.True);
            Assert.That(validator.Validate(new Order(0, 50, "open", "BRL")).IsValid, Is.False);
            Assert.That(validator.Validate(new Order(1, 51, "open", "BRL")).IsValid, Is.False);
            Assert.That(validator.Validate(new Order(1, 50, "deleted", "BRL")).IsValid, Is.False);
            Assert.That(validator.Validate(new Order(1, 50, "open", "GBP")).IsValid, Is.False);
            Assert.That(validator.Validate(new Order(1, 50, "open", null)).IsValid, Is.True,
                "OneOf is null-permissive; combine with NotNull to require a value");
        });
    }

    [Test]
    public void Shortcuts_flow_into_the_manifest_and_zod_export()
    {
        var validator = new InlineValidator<Order>(v =>
        {
            v.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
            v.RuleFor(x => x.Discount).LessThanOrEqualTo(50);
        });

        var description = validator.Describe();
        var schema = ZodSchemaExporter.Export(description);

        Assert.Multiple(() =>
        {
            Assert.That(description.Rules.Select(rule => rule.Kind), Does.Contain(ValidationRuleKinds.GreaterThanOrEqual));
            Assert.That(description.Rules.Select(rule => rule.Kind), Does.Contain(ValidationRuleKinds.LessThanOrEqual));
            Assert.That(schema, Does.Contain("quantity: z.number().int().gte(1),"));
            Assert.That(schema, Does.Contain("discount: z.number().int().lte(50),"));
        });
    }

    [Test]
    public void InlineValidator_declares_rules_without_a_subclass()
    {
        var validator = new InlineValidator<Order>();
        validator.RuleFor(x => x.Status).NotWhiteSpace();
        validator.RuleForModel()
            .Match(
                static order => order is { Quantity: 0, Status: "open" },
                targetPropertyPath: nameof(Order.Quantity),
                code: "order.quantity.zero_open",
                messageTemplate: "Open orders need at least one item.");

        var result = validator.Validate(new Order(0, 0, "open", "BRL"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single().Code, Is.EqualTo("order.quantity.zero_open"));
        });
    }

    [Test]
    public void RuleFor_uses_registered_accessors_instead_of_compiling_expressions()
    {
        var hits = 0;
        ValidatorAccessors.Register<Tracked, string?>("Name", x =>
        {
            hits++;
            return x.Name;
        });

        var validator = new InlineValidator<Tracked>(v => v.RuleFor(x => x.Name).NotWhiteSpace());
        var result = validator.Validate(new Tracked("ok"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(hits, Is.EqualTo(1), "the registered accessor must be used instead of Expression.Compile");
        });
    }

    private sealed record Order(int Quantity, int Discount, string? Status, string? Currency);
    private sealed record Tracked(string? Name);
}
