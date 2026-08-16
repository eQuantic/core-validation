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
        RuleForEach(x => x.Items).SetValidator(new OrderItemValidator());
    }
}

var result = await new CreateOrderValidator().ValidateAsync(order, cancellationToken: cancellationToken);
```

Use fluent rules for domain logic, cross-field pattern matching, Value Objects, and service-dependent rules. For DTOs with `System.ComponentModel.DataAnnotations`, instantiate or register `AttributeValidator<T>` to produce the same structured `ValidationResult`.

This package implements `eQuantic.Validation.Abstractions` and has zero dependency on ASP.NET Core. For web endpoints and controllers, use `eQuantic.Validation.AspNetCore`.

Complete documentation: <https://github.com/eQuantic/core-validation>
