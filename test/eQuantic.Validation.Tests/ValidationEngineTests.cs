using System.ComponentModel.DataAnnotations;
using eQuantic.Validation.AspNetCore;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class ValidationEngineTests
{
    [Test]
    public async Task ValidateAsync_returns_structured_failures_for_fluent_rules()
    {
        var validator = new RegistrationValidator();

        var result = await validator.ValidateAsync(new Registration("  ", "short", new Address("", "12"), Array.Empty<Contact>()));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Email"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Password"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Address.Street"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Address.PostalCode"));
            Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Contacts"));
            Assert.That(result.Errors.Single(error => error.Code == "customer.email.invalid").Path, Is.EqualTo("Email"));
            Assert.That(result.ToErrorDictionary()["Email"], Does.Contain("Email must not be blank."));
        });
    }

    [Test]
    public void Validate_keeps_warning_rules_non_blocking()
    {
        var validator = new WarningValidator();

        var result = validator.Validate(new WarningModel("legacy"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Warnings, Has.Count.EqualTo(1));
            Assert.That(result.Warnings[0].Code, Is.EqualTo("deprecated.value"));
        });
    }

    [Test]
    public async Task ValidateAsync_honors_scenarios_and_cancellation_aware_rules()
    {
        var validator = new RegistrationValidator();
        var input = new Registration("hello@example.com", "correct-password", new Address("Main", "12345"), new[] { new Contact("valid@example.com") });

        var createResult = await validator.ValidateAsync(input, ValidationContext.ForScenarios("create"));
        var updateResult = await validator.ValidateAsync(input, ValidationContext.ForScenarios("update"));

        Assert.Multiple(() =>
        {
            Assert.That(createResult.Errors.Select(error => error.Code), Does.Contain("customer.email.taken"));
            Assert.That(updateResult.IsValid, Is.True);
        });
    }

    [Test]
    public async Task ValidateAsync_scopes_nested_validation_to_selected_paths()
    {
        var validator = new RegistrationValidator();
        var input = new Registration("hello@example.com", "correct-password", new Address("", "12"), new[] { new Contact("not-an-email") });

        var result = await validator.ValidateAsync(input, ValidationContext.ForPaths("Address.PostalCode"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo("Address.PostalCode"));
        });
    }

    [Test]
    public async Task ValidateAsync_scopes_collection_validation_to_the_selected_element()
    {
        var validator = new RegistrationValidator();
        var input = new Registration(
            "hello@example.com",
            "correct-password",
            new Address("Main", "12345"),
            new[] { new Contact("not-an-email"), new Contact("still-not-an-email") });

        var result = await validator.ValidateAsync(input, ValidationContext.ForPaths("Contacts[1].Email"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo("Contacts[1].Email"));
        });
    }

    [Test]
    public void Validate_requires_async_api_when_an_active_rule_is_async()
    {
        var validator = new RegistrationValidator();
        var input = new Registration("hello@example.com", "correct-password", new Address("Main", "12345"), new[] { new Contact("valid@example.com") });

        Assert.That(
            () => validator.Validate(input, ValidationContext.ForScenarios("create")),
            Throws.TypeOf<AsyncValidationRequiredException>());
    }

    [Test]
    public void AttributeValidator_adapts_standard_data_annotations()
    {
        var validator = new AttributeValidator<AnnotatedRequest>();

        var result = validator.Validate(new AnnotatedRequest { Email = "not-an-email" });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(nameof(AnnotatedRequest.Email)));
            Assert.That(result.Errors[0].Code, Is.EqualTo(ValidationCodes.Attribute));
        });
    }

    [Test]
    public async Task Dispatcher_resolves_explicitly_registered_validators_from_the_scope()
    {
        var services = new ServiceCollection();
        services.AddValidationDispatcher().AddValidator<Registration, RegistrationValidator>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();
        var result = await dispatcher.ValidateAsync(
            new Registration("", "correct-password", new Address("Main", "12345"), new[] { new Contact("valid@example.com") }),
            scope.ServiceProvider);

        Assert.That(result.Errors.Select(error => error.Path), Does.Contain("Email"));
    }

    [Test]
    public void PatternMatching_evaluates_relational_cross_field_rules()
    {
        var validator = new PaymentValidator();

        var invalidPix = new Payment("PIX", 100, "1234-5678-9012-3456");
        var validPix = new Payment("PIX", 100, null);
        var invalidCredit = new Payment("CREDIT", 0, "1234-5678-9012-3456");

        var pixResult = validator.Validate(invalidPix);
        var validPixResult = validator.Validate(validPix);
        var creditResult = validator.Validate(invalidCredit);

        Assert.Multiple(() =>
        {
            Assert.That(pixResult.IsValid, Is.False);
            Assert.That(pixResult.Errors.Single().Path, Is.EqualTo("CardNumber"));
            Assert.That(pixResult.Errors.Single().Code, Is.EqualTo("payment.pix.no_card"));

            Assert.That(validPixResult.IsValid, Is.True);

            Assert.That(creditResult.IsValid, Is.False);
            Assert.That(creditResult.Errors.Single().Code, Is.EqualTo("payment.credit.amount"));
        });
    }

    [Test]
    public void MustCreate_validates_domain_value_object_instantiation()
    {
        var validator = new ValueObjectModelValidator();

        var validModel = new ValueObjectModel("user@example.com", "100.50", "47c7c006-2c9e-4c12-8fe3-4f96d071a179");
        var invalidEmail = new ValueObjectModel("invalid-email", "100.50");
        var invalidAmount = new ValueObjectModel("user@example.com", "-50");
        var invalidGuid = new ValueObjectModel("user@example.com", "100.50", "invalid-guid");

        var validResult = validator.Validate(validModel);
        var invalidEmailResult = validator.Validate(invalidEmail);
        var invalidAmountResult = validator.Validate(invalidAmount);
        var invalidGuidResult = validator.Validate(invalidGuid);

        Assert.Multiple(() =>
        {
            Assert.That(validResult.IsValid, Is.True);

            Assert.That(invalidEmailResult.IsValid, Is.False);
            Assert.That(invalidEmailResult.Errors.Single().Path, Is.EqualTo("Email"));
            Assert.That(invalidEmailResult.Errors.Single().Code, Is.EqualTo("vo.email.invalid"));

            Assert.That(invalidAmountResult.IsValid, Is.False);
            Assert.That(invalidAmountResult.Errors.Single().Path, Is.EqualTo("Amount"));
            Assert.That(invalidAmountResult.Errors.Single().Code, Is.EqualTo("vo.money.invalid"));

#if NET7_0_OR_GREATER
            Assert.That(invalidGuidResult.IsValid, Is.False);
            Assert.That(invalidGuidResult.Errors.Single().Path, Is.EqualTo("TransactionId"));
            Assert.That(invalidGuidResult.Errors.Single().Code, Is.EqualTo("vo.guid.invalid"));
#endif
        });
    }

    private sealed record ValueObjectModel(string Email, string Amount, string? TransactionId = null);

    private sealed class EmailVo
    {
        public string Value { get; }
        private EmailVo(string value) => Value = value;

        public static EmailVo Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.Contains("@"))
            {
                throw new ArgumentException("Invalid email format.", nameof(value));
            }
            return new EmailVo(value);
        }
    }

    private sealed class ValueObjectModelValidator : Validator<ValueObjectModel>
    {
        public ValueObjectModelValidator()
        {
            RuleFor(x => x.Email)
                .MustCreate(
                    static raw => EmailVo.Create(raw),
                    code: "vo.email.invalid",
                    messageTemplate: "Email address could not be created as a Value Object.");

            RuleFor(x => x.Amount)
                .MustCreate(
                    static raw => decimal.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var val) && val > 0,
                    code: "vo.money.invalid",
                    messageTemplate: "Money amount is invalid.");

#if NET7_0_OR_GREATER
            RuleFor(x => x.TransactionId)
                .MustParse<Guid>(
                    code: "vo.guid.invalid",
                    messageTemplate: "TransactionId must be a valid GUID.");
#endif
        }
    }

    private sealed record Payment(string Method, decimal Amount, string? CardNumber);

    private sealed class PaymentValidator : Validator<Payment>
    {
        public PaymentValidator()
        {
            RuleForModel()
                .Match(
                    static p => p is { Method: "PIX", CardNumber: not null },
                    targetPropertyPath: nameof(Payment.CardNumber),
                    code: "payment.pix.no_card",
                    messageTemplate: "Pix payment must not contain a card number.")
                .Match(
                    static p => p is { Method: "CREDIT", Amount: <= 0 },
                    targetPropertyPath: nameof(Payment.Amount),
                    code: "payment.credit.amount",
                    messageTemplate: "Credit payment amount must be greater than zero.");
        }
    }

    private sealed class RegistrationValidator : Validator<Registration>
    {
        public RegistrationValidator()
        {
            RuleFor(model => model.Email)
                .NotWhiteSpace()
                .Email()
                .WithCode("customer.email.invalid");

            RuleFor(model => model.Email)
                .MustAsync(
                    static (_, email, _, cancellationToken) => IsEmailAvailableAsync(email, cancellationToken),
                    "customer.email.taken")
                .ForScenarios("create");

            RuleFor(model => model.Password)
                .MinimumLength(12)
                .StopOnFirstFailure();

            RuleFor(model => model.Address).SetValidator(new AddressValidator());
            RuleForEach(model => model.Contacts).SetValidator(new ContactValidator());
            RuleFor(model => model.Contacts).NotEmpty();
        }

        private static async Task<bool> IsEmailAvailableAsync(string? email, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return !string.Equals(email, "hello@example.com", StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class AddressValidator : Validator<Address>
    {
        public AddressValidator()
        {
            RuleFor(model => model.Street).NotWhiteSpace();
            RuleFor(model => model.PostalCode).Matches("^[0-9]{5}$");
        }
    }

    private sealed class ContactValidator : Validator<Contact>
    {
        public ContactValidator()
        {
            RuleFor(model => model.Email).Email();
        }
    }

    private sealed class WarningValidator : Validator<WarningModel>
    {
        public WarningValidator()
        {
            RuleFor(model => model.Value)
                .Must(static value => value != "legacy", "deprecated.value")
                .WithSeverity(ValidationSeverity.Warning);
        }
    }

    private sealed record Registration(string? Email, string Password, Address Address, IReadOnlyList<Contact> Contacts);
    private sealed record Address(string? Street, string PostalCode);
    private sealed record Contact(string? Email);
    private sealed record WarningModel(string Value);

    private sealed class AnnotatedRequest
    {
        [Required]
        [EmailAddress]
        public string? Email { get; init; }
    }
}
