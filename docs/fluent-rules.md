# Fluent rules reference

All rules are **null-permissive** unless stated otherwise: a `null` value passes shape rules, and
presence is demanded explicitly. This keeps `Required` concerns separate from shape concerns.

```csharp
RuleFor(x => x.Email).NotNull().Email();   // presence + shape, each explicit
```

## Presence

```csharp
RuleFor(x => x.Name).NotNull();        // non-null                          code: required
RuleFor(x => x.Tags).NotEmpty();       // non-empty string/collection       code: not_empty
RuleFor(x => x.Name).NotWhiteSpace();  // non-blank string                  code: not_whitespace
```

## Strings (compile-time typed — these only bind to `string` properties)

```csharp
RuleFor(x => x.Email).Email();
RuleFor(x => x.Name).MinimumLength(2).MaximumLength(60);
RuleFor(x => x.Nick).Length(3, 12);
RuleFor(x => x.Zip).Matches("^[0-9]{5}$");    // pattern flows into OpenAPI/Zod
```

Calling `.Email()` on an `int` property is a compile error, not a runtime surprise.

## Comparisons — against values

```csharp
RuleFor(x => x.Age).InclusiveBetween(18, 120);
RuleFor(x => x.Amount).GreaterThan(0m);
RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).LessThanOrEqualTo(50);
RuleFor(x => x.Status).EqualTo("open");
RuleFor(x => x.Status).NotEqualTo("deleted");
RuleFor(x => x.Currency).OneOf("BRL", "EUR", "USD");
```

## Comparisons — against other members (cross-property)

```csharp
RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate);
RuleFor(x => x.MaxSeats).GreaterThanOrEqualTo(x => x.MinSeats);
RuleFor(x => x.ConfirmPassword).EqualTo(x => x.Password);   // strict: null ≠ "secret"
```

The failure message names the other member: `"EndDate must be greater than StartDate."`.

## Cross-field pattern matching

`Match` fails when the *invalid* pattern is matched, and reports on the path you choose:

```csharp
RuleForModel()
    .Match(
        static p => p is { Method: "PIX", CardNumber: not null },
        targetPropertyPath: nameof(Payment.CardNumber),
        code: "payment.pix.no_card",
        messageTemplate: "Pix payment must not contain a card number.");
```

## Custom predicates

```csharp
RuleFor(x => x.Sku).Must(sku => sku!.StartsWith("ITEM-"), code: "item.sku.prefix");

RuleFor(x => x.Email).MustAsync(
    async (model, email, context, ct) => !await directory.ExistsAsync(email!, ct),
    code: "customer.email.taken");
```

Async rules require `ValidateAsync`; within one operation the same async rule for the same
instance and value runs once (see [ASP.NET Core → deduplication](aspnetcore.md)).

## Value Objects and parsable types

```csharp
RuleFor(x => x.Email).MustCreate(raw => EmailVo.Create(raw), code: "vo.email.invalid");
RuleFor(x => x.Id).MustParse<Guid>(code: "id.invalid");     // any IParsable<T>
```

`MustCreate` passes when the factory returns non-null without throwing.

## Nested objects and collections

```csharp
RuleFor(x => x.Address).SetValidator(new AddressValidator());       // reuse a validator
RuleFor(x => x.Address).ChildRules(a =>                             // or declare inline
    a.RuleFor(n => n.Street).NotWhiteSpace());

RuleForEach(x => x.Contacts).SetValidator(new ContactValidator());
RuleForEach(x => x.Items).ChildRules(item =>
{
    item.RuleFor(i => i.Sku).NotWhiteSpace();
    item.RuleFor(i => i.Quantity).GreaterThan(0);
});
```

Failure paths are canonical: `Address.Street`, `Items[2].Sku`.

## Composition, conditions and scenarios

```csharp
Include(new CommonRulesValidator());                    // merge another validator's rules

RuleFor(x => x.CreditLimit)
    .GreaterThan(0m)
    .When(x => x.IsVip);                                // rule runs only when true

RuleFor(x => x.Email)
    .MustAsync(CheckUniqueness, "customer.email.taken")
    .ForScenarios("create");                            // only for the "create" scenario

RuleFor(x => x.Password).MinimumLength(12).StopOnFirstFailure();
```

Run scenarios with `ValidationContext.ForScenarios("create")` or `.RequireValidation("create")`;
validate a subset of paths (PATCH) with `ValidationContext.ForPaths("Address.City")`.

## Messages, codes and severities

```csharp
RuleFor(x => x.Email)
    .Email()
    .WithCode("customer.email.invalid")     // stable machine-readable code
    .WithMessage("Please review the email address.")
    .WithName("E-mail");                    // display name used by {Property}

RuleFor(x => x.Nickname)
    .NotEqualTo("legacy")
    .WithSeverity(ValidationSeverity.Warning);   // valid, but tell the caller
```

Warnings never block (`IsValid` stays `true`) and reach HTTP clients on successful responses —
see [ASP.NET Core](aspnetcore.md).
