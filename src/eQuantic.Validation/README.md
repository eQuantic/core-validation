# eQuantic.Validation

O motor de validação independente de framework da família eQuantic.

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

Use regras fluent para lógica de domínio e regras que dependem de serviços. Para DTOs que já usam
`System.ComponentModel.DataAnnotations`, registre ou instancie `AttributeValidator<T>` e obtenha o
mesmo `ValidationResult` estruturado.

O pacote implementa `eQuantic.Validation.Abstractions` e não tem dependência de ASP.NET Core.
Para endpoints e controllers use `eQuantic.Validation.AspNetCore`.

Documentação completa: <https://github.com/eQuantic/core-validation>
