# eQuantic.Validation

Framework-independent fluent validation engine for the eQuantic ecosystem.

```bash
dotnet add package eQuantic.Validation
```

```csharp
public sealed class CreateOrderValidator : Validator<CreateOrder>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerEmail).NotWhiteSpace().Email();
        RuleFor(x => x.Total).GreaterThan(0m);
        RuleFor(x => x.Installments).GreaterThanOrEqualTo(1).LessThanOrEqualTo(12);
        RuleFor(x => x.Currency).NotNull().OneOf("BRL", "EUR", "USD");
        RuleForEach(x => x.Items).SetValidator(new OrderItemValidator());
    }
}

var result = await new CreateOrderValidator().ValidateAsync(order, cancellationToken: cancellationToken);
```

Use fluent rules for domain logic, cross-field pattern matching, Value Objects, and service-dependent rules. String rules (`Email`, `MinimumLength`, `Matches`, …) only bind to `string` properties, failing at compile time elsewhere. For DTOs with `System.ComponentModel.DataAnnotations`, instantiate or register `AttributeValidator<T>` to produce the same structured `ValidationResult`.

### No subclass? Use InlineValidator

```csharp
var validator = new InlineValidator<Customer>(v =>
{
    v.RuleFor(x => x.Email).NotWhiteSpace().Email();
    v.RuleFor(x => x.Age).InclusiveBetween(18, 120);
});
```

### Warnings and severities

Rules can warn without blocking; `IsValid` ignores warnings and they stay available on the result:

```csharp
RuleFor(x => x.Nickname)
    .Must(n => n != "legacy", "profile.nickname.legacy")
    .WithSeverity(ValidationSeverity.Warning);

var result = validator.Validate(profile);
// result.IsValid == true; result.Warnings => [ { Path: "Nickname", Code: "profile.nickname.legacy" } ]
```

### Rules as data

Every validator implements `IDescribableValidator`: `Describe()` returns the rule manifest
(paths, kinds, codes, parameters), and `ZodSchemaExporter` turns it into a client-side schema:

```csharp
var manifest = new CreateOrderValidator().Describe();
string ts = eQuantic.Validation.Export.ZodSchemaExporter.Export(new CreateOrderValidator());
// export const createOrderSchema = z.object({ customerEmail: z.string().min(1).email(), ... });
```

### Async rules that don't repeat I/O

With `new ValidationContext(deduplicateAsyncRules: true)` (the ASP.NET Core integrations enable
this per request automatically), the same async rule for the same instance and value runs once —
composed validators, duplicated collection elements and revalidation reuse the first result.

Dependency-injection dispatching ships in this package (`AddValidationDispatcher`, `AddValidator<TModel, TValidator>` and `IValidationDispatcher`), so workers, gRPC services and console apps can validate without referencing ASP.NET Core — the only dependency is `Microsoft.Extensions.DependencyInjection.Abstractions`. OpenTelemetry instrumentation (`eQuantic.Validation` meter and activity source) is built in.

This package implements `eQuantic.Validation.Abstractions`. For web endpoints and controllers, use `eQuantic.Validation.AspNetCore`; for compile-time validators, accessor pre-compilation and usage diagnostics, add `eQuantic.Validation.Generator`.

Complete documentation: <https://github.com/eQuantic/core-validation>
