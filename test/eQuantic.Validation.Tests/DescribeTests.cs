namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class DescribeTests
{
    [Test]
    public void Describe_lists_fluent_rules_with_paths_kinds_and_arguments()
    {
        var description = new AccountValidator().Describe();

        Assert.Multiple(() =>
        {
            Assert.That(description.ModelType, Is.EqualTo(typeof(Account)));

            var emailRules = description.Rules.Where(rule => rule.Path == "Email").ToArray();
            Assert.That(emailRules.Select(rule => rule.Kind), Is.EquivalentTo(new[]
            {
                ValidationRuleKinds.NotWhiteSpace,
                ValidationRuleKinds.Email,
                ValidationRuleKinds.Predicate,
            }));

            var nameLength = description.Rules.Single(rule => rule.Kind == ValidationRuleKinds.MinimumLength);
            Assert.That(nameLength.Path, Is.EqualTo("Name"));
            Assert.That(nameLength.Arguments["MinimumLength"], Is.EqualTo(2));

            var pattern = description.Rules.Single(rule => rule.Kind == ValidationRuleKinds.Pattern);
            Assert.That(pattern.Arguments["Pattern"], Is.EqualTo("^[0-9]{5}$"));

            var age = description.Rules.Single(rule => rule.Kind == ValidationRuleKinds.InclusiveBetween);
            Assert.That(age.Arguments["Minimum"], Is.EqualTo(18));
            Assert.That(age.Arguments["Maximum"], Is.EqualTo(120));
        });
    }

    [Test]
    public void Describe_marks_async_scenario_and_conditional_rules()
    {
        var description = new AccountValidator().Describe();

        Assert.Multiple(() =>
        {
            var uniqueness = description.Rules.Single(rule => rule.Code == "account.email.taken");
            Assert.That(uniqueness.IsAsync, Is.True);
            Assert.That(uniqueness.Scenarios, Is.EquivalentTo(new[] { "create" }));

            var conditional = description.Rules.Single(rule => rule.Code == "account.vip.limit");
            Assert.That(conditional.IsConditional, Is.True);
        });
    }

    [Test]
    public void Describe_expands_nested_and_collection_validators_with_prefixed_paths()
    {
        var description = new AccountValidator().Describe();

        Assert.Multiple(() =>
        {
            Assert.That(description.Rules.Select(rule => rule.Path), Does.Contain("Address.Street"));
            Assert.That(description.Rules.Select(rule => rule.Path), Does.Contain("Contacts[].Email"));
        });
    }

    [Test]
    public void Describe_uses_target_path_for_cross_field_match_rules()
    {
        var description = new AccountValidator().Describe();

        var match = description.Rules.Single(rule => rule.Kind == ValidationRuleKinds.Match);
        Assert.That(match.Path, Is.EqualTo("CreditLimit"));
    }

    [Test]
    public void Generated_validators_expose_the_same_manifest_shape()
    {
        IDescribableValidator validator = new SourceGenUserGeneratedValidator();

        var description = validator.Describe();

        Assert.Multiple(() =>
        {
            Assert.That(description.ModelType, Is.EqualTo(typeof(SourceGenUser)));

            var email = description.Rules.Single(rule => rule.Kind == ValidationRuleKinds.Email);
            Assert.That(email.Path, Is.EqualTo("Email"));
            Assert.That(email.Code, Is.EqualTo("custom.email.invalid"));

            var age = description.Rules.Single(rule => rule.Kind == ValidationRuleKinds.InclusiveBetween);
            Assert.That(age.Path, Is.EqualTo("Age"));
            Assert.That(age.Arguments["Minimum"], Is.EqualTo(18L));
            Assert.That(age.Arguments["Maximum"], Is.EqualTo(120L));

            Assert.That(description.Rules.Select(rule => rule.Kind), Does.Contain(ValidationRuleKinds.Required));
        });
    }

    private sealed record Account(
        string? Name,
        string? Email,
        string? PostalCode,
        int Age,
        bool IsVip,
        decimal CreditLimit,
        Address? Address,
        IReadOnlyList<Contact> Contacts);

    private sealed record Address(string? Street);
    private sealed record Contact(string? Email);

    private sealed class AccountValidator : Validator<Account>
    {
        public AccountValidator()
        {
            RuleFor(x => x.Name).NotNull().MinimumLength(2);
            RuleFor(x => x.Email).NotWhiteSpace().Email();
            RuleFor(x => x.Email)
                .MustAsync(static (_, _, _, _) => Task.FromResult(true), "account.email.taken")
                .ForScenarios("create");
            RuleFor(x => x.PostalCode).Matches("^[0-9]{5}$");
            RuleFor(x => x.Age).InclusiveBetween(18, 120);
            RuleFor(x => x.CreditLimit)
                .GreaterThan(0)
                .WithCode("account.vip.limit")
                .When(static account => account.IsVip);
            RuleForModel()
                .Match(
                    static account => account is { IsVip: true, CreditLimit: < 5000 },
                    targetPropertyPath: nameof(Account.CreditLimit),
                    code: "account.vip.minimum",
                    messageTemplate: "VIP accounts require a credit limit of at least 5000.");
            RuleFor(x => x.Address!).SetValidator(new AddressValidator());
            RuleForEach(x => x.Contacts).SetValidator(new ContactValidator());
        }
    }

    private sealed class AddressValidator : Validator<Address>
    {
        public AddressValidator()
        {
            RuleFor(x => x.Street).NotWhiteSpace();
        }
    }

    private sealed class ContactValidator : Validator<Contact>
    {
        public ContactValidator()
        {
            RuleFor(x => x.Email).Email();
        }
    }
}
