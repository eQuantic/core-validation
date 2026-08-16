using eQuantic.Validation.Testing;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class TestingPackageTests
{
    private static readonly SignupValidator Validator = new();

    [Test]
    public void ShouldHaveFailureFor_matches_by_string_path_and_typed_expression()
    {
        var result = Validator.TestValidate(new Signup("", "user@example.com", new Address("")));

        Assert.That(() => result.ShouldHaveFailureFor("Name"), Throws.Nothing);
        Assert.That(() => result.ShouldHaveFailureFor(x => x.Name), Throws.Nothing);
        Assert.That(() => result.ShouldHaveFailureFor(x => x.Address!.Street), Throws.Nothing);
    }

    [Test]
    public void Chained_filters_narrow_by_code_severity_and_argument()
    {
        var result = Validator.TestValidate(new Signup("A", "user@example.com", new Address("Main")));

        Assert.That(
            () => result
                .ShouldHaveFailureFor(x => x.Name)
                .WithCode(ValidationCodes.MinimumLength)
                .WithSeverity(ValidationSeverity.Error)
                .WithArgument("MinimumLength", 2)
                .Exactly(1),
            Throws.Nothing);
    }

    [Test]
    public void Failed_assertions_throw_with_the_actual_failures_in_the_message()
    {
        var result = Validator.TestValidate(new Signup("", "not-an-email", new Address("Main")));

        var missingPath = Assert.Throws<ValidationAssertionException>(
            () => result.ShouldHaveFailureFor("Nope"));
        var wrongCode = Assert.Throws<ValidationAssertionException>(
            () => result.ShouldHaveFailureFor("Email").WithCode("wrong.code"));

        Assert.Multiple(() =>
        {
            Assert.That(missingPath!.Message, Does.Contain("Expected a failure for path 'Nope'"));
            Assert.That(missingPath.Message, Does.Contain("[not_whitespace]").Or.Contain("[email]"));
            Assert.That(wrongCode!.Message, Does.Contain("with code 'wrong.code'"));
            Assert.That(wrongCode.Message, Does.Contain("[email]"));
        });
    }

    [Test]
    public void ShouldBeValid_allows_warnings_and_ShouldBeInvalid_requires_errors()
    {
        var withWarning = Validator.TestValidate(new Signup("Legacy User", "user@example.com", new Address("Main")));

        Assert.Multiple(() =>
        {
            Assert.That(() => withWarning.ShouldBeValid(), Throws.Nothing);
            Assert.That(
                () => withWarning.ShouldHaveFailureFor(x => x.Name).WithSeverity(ValidationSeverity.Warning),
                Throws.Nothing);
            Assert.That(
                () => withWarning.ShouldBeInvalid(),
                Throws.TypeOf<ValidationAssertionException>());
        });
    }

    [Test]
    public void ShouldNotHaveFailureFor_passes_for_clean_paths_and_fails_for_dirty_ones()
    {
        var result = Validator.TestValidate(new Signup("Valid Name", "broken", new Address("Main")));

        Assert.Multiple(() =>
        {
            Assert.That(() => result.ShouldNotHaveFailureFor(x => x.Name), Throws.Nothing);
            Assert.That(
                () => result.ShouldNotHaveFailureFor(x => x.Email),
                Throws.TypeOf<ValidationAssertionException>());
        });
    }

    [Test]
    public async Task Scenario_overloads_and_async_rules_flow_through()
    {
        var taken = new Signup("Valid Name", "taken@example.com", new Address("Main"));

        var createResult = await Validator.TestValidateAsync(taken, "create");
        var updateResult = await Validator.TestValidateAsync(taken, "update");

        Assert.Multiple(() =>
        {
            Assert.That(
                () => createResult.ShouldHaveFailureWithCode("signup.email.taken"),
                Throws.Nothing);
            Assert.That(() => updateResult.ShouldBeValid(), Throws.Nothing);
        });
    }

    [Test]
    public void Collection_paths_use_canonical_indexing()
    {
        var validator = new TeamValidator();
        var result = validator.TestValidate(new Team(new[] { new Member("ok@example.com"), new Member("bad") }));

        Assert.That(() => result.ShouldHaveFailureFor("Members[1].Contact"), Throws.Nothing);
    }

    private sealed record Signup(string? Name, string? Email, Address? Address);
    private sealed record Address(string? Street);
    private sealed record Team(IReadOnlyList<Member> Members);
    private sealed record Member(string? Contact);

    private sealed class SignupValidator : Validator<Signup>
    {
        public SignupValidator()
        {
            RuleFor(x => x.Name).NotWhiteSpace().MinimumLength(2);
            RuleFor(x => x.Name)
                .Must(static name => name != "Legacy User", "signup.name.legacy")
                .WithSeverity(ValidationSeverity.Warning);
            RuleFor(x => x.Email).NotWhiteSpace().Email();
            RuleFor(x => x.Email)
                .MustAsync(
                    static (_, email, _, _) => Task.FromResult(!string.Equals(email, "taken@example.com", StringComparison.OrdinalIgnoreCase)),
                    "signup.email.taken")
                .ForScenarios("create");
            RuleFor(x => x.Address!).SetValidator(new AddressValidator());
        }
    }

    private sealed class AddressValidator : Validator<Address>
    {
        public AddressValidator()
        {
            RuleFor(x => x.Street).NotWhiteSpace();
        }
    }

    private sealed class TeamValidator : Validator<Team>
    {
        public TeamValidator()
        {
            RuleForEach(x => x.Members).SetValidator(new MemberValidator());
        }
    }

    private sealed class MemberValidator : Validator<Member>
    {
        public MemberValidator()
        {
            RuleFor(x => x.Contact).Email();
        }
    }
}
