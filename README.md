# eQuantic.Validation

[![CI](https://github.com/equantic/core-validation/actions/workflows/ci.yml/badge.svg)](https://github.com/equantic/core-validation/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/eQuantic.Validation.svg)](https://www.nuget.org/packages/eQuantic.Validation/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Validation moderna para .NET 10: validações geradas em tempo de compilação (Zero-Allocation), regras relacionais com Pattern Matching, OpenAPI 3.1 integrado, observabilidade nativa com OpenTelemetry, i18n e suporte total a Native AOT.**

---

## ⚡ Três formas inovadoras de validar:

### 1. Validação Declarativa com Source Generator (Zero-Allocation & Native AOT)

Elimina a necessidade de classes manuais para DTOs e Commands simples. O Roslyn Source Generator emite o código procedural em tempo de compilação — **zero reflection, zero expression trees e zero boxing**:

```csharp
using eQuantic.Validation.Attributes;

[GenerateValidator]
public sealed record CreateCustomer(
    [Required, NotWhiteSpace] string Name,
    [Required, Email(Code = "customer.email.invalid")] string Email,
    [Range(18, 120)] int Age,
    [ValidateNested] AddressDto Address,
    [ValidateEach] IReadOnlyList<ContactDto> Contacts
);
```

### 2. Validação Fluente Code-First com Pattern Matching

Permite expressar regras cross-field e condicionais complexas usando a sintaxe moderna de Pattern Matching do C#:

```csharp
public sealed class PaymentValidator : Validator<PaymentRequest>
{
    public PaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);

        // Pattern Matching Relacional moderno:
        RuleForModel()
            .Match(
                static p => p is { Method: "PIX", CardNumber: not null },
                targetPropertyPath: nameof(PaymentRequest.CardNumber),
                code: "payment.pix.no_card",
                messageTemplate: "Pix payment must not contain a card number.")
            .Match(
                static p => p is { Method: "PIX", PixKey: null },
                targetPropertyPath: nameof(PaymentRequest.PixKey),
                code: "payment.pix.key_required",
                messageTemplate: "Pix key is required for Pix payments.");
    }
}
```

### 3. Validação de Value Objects (DDD) & Tipos Parsable

Valide a criação de Value Objects do seu domínio sem acoplar a camada de validação:

```csharp
public sealed class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        // Factory de Value Object (falha se lançar exceção ou retornar null):
        RuleFor(x => x.Email)
            .MustCreate(
                email => EmailVo.Create(email),
                code: "customer.email.invalid",
                messageTemplate: "Invalid email format.");

        // IParsable nativo do .NET (Guid, DateOnly, Money, etc.):
        RuleFor(x => x.TransactionId)
            .MustParse<Guid>(
                code: "customer.transaction_id.invalid",
                messageTemplate: "Transaction ID must be a valid GUID.");
    }
}
```

### 4. Regras Assíncronas e I/O com Injeção de Dependência

```csharp
public sealed class CreateCustomerValidator : Validator<CreateCustomer>
{
    public CreateCustomerValidator(ICustomerDirectory customers)
    {
        RuleFor(x => x.Email)
            .NotWhiteSpace()
            .Email()
            .MustAsync(
                async (_, email, _, cancellationToken) =>
                    !await customers.EmailExistsAsync(email!, cancellationToken),
                code: "customer.email.taken")
            .WithMessage("This email address is already in use.");

        RuleFor(x => x.Address).SetValidator(new AddressValidator());
        RuleForEach(x => x.Contacts).SetValidator(new ContactValidator());
    }
}
```

---

## 🚀 Integração com ASP.NET Core & Minimal APIs (.NET 10)

```csharp
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Generated;

var builder = WebApplication.CreateBuilder(args);

// 1. Injeção de dependência automática gerada no build
builder.Services.AddGeneratedValidation();

// 2. Localização / i18n automática (Accept-Language)
builder.Services.AddValidationLocalization<ValidationResources>();

// 3. Integração com OpenAPI 3.1 (Scalar / Swagger)
builder.Services.AddOpenApi(options =>
{
    options.AddValidationTransformer();
});

var app = builder.Build();

app.MapPost("/customers", (CreateCustomer command) => Results.Created())
   .RequireValidation("create");
```

---

## 📊 Observabilidade & Métricas com OpenTelemetry

Métricas nativas (`System.Diagnostics.Metrics`) e distributed tracing (`ActivitySource`) com **Zero PII**:

- `validation.requests.total`: Contador de validações por tipo e resultado.
- `validation.failures.total`: Breakdown de falhas por código de erro estável.
- `validation.duration.ms`: Histograma de latência em milissegundos.

---

## 🏎️ Benchmarks Científicos (BenchmarkDotNet no .NET 10)

Resultados oficiais executados com `BenchmarkDotNet v0.14.0` no **.NET 10.0** (Apple M4 Pro Arm64):

| Método / Abordagem | Cenário | Tempo Médio (`Mean`) | Memória Alocada (`Allocated`) | Performance vs FluentValidation |
| :--- | :---: | :---:| :---: | :---: |
| 🥇 **eQuantic (Source Generated)** | **Válido** | **87.22 ns** | **32 B** | 🚀 **2.7x mais rápido / 95% menos memória** |
| 🥈 **eQuantic (Fluent DSL)** | **Válido** | **136.85 ns** | **160 B** | ⚡ **1.7x mais rápido / 77% menos memória** |
| 🥉 **FluentValidation** | **Válido** | **235.36 ns** | **696 B** | *Baseline* |
| | | | | |
| 🥇 **eQuantic (Source Generated)** | **Inválido (Erros)** | **175.89 ns** | **1.168 B** | 🚀 **16.5x mais rápido / 91% menos memória** |
| 🥈 **eQuantic (Fluent DSL)** | **Inválido (Erros)** | **1.108 µs** | **5.536 B** | ⚡ **2.6x mais rápido / 56% menos memória** |
| 🥉 **FluentValidation** | **Inválido (Erros)** | **2.899 µs** | **12.600 B** | *Baseline* |

> *Para reproduzir os testes em sua máquina, execute `dotnet run -c Release --project benchmarks/eQuantic.Validation.Benchmarks`.*

---

## 🌟 Comparativo de Recursos: eQuantic.Validation vs. FluentValidation

| Recurso | FluentValidation tradicional | eQuantic.Validation |
| --- | --- | --- |
| **Geração de Código (Build)** | ❌ Apenas runtime | ✅ **Zero-Allocation Source Generator** com `[GenerateValidator]` |
| **Pattern Matching Cross-Field** | ❌ `.When(...)` aninhado e verboso | ✅ **`RuleForModel().Match(p => p is { ... })`** idiomático |
| **OpenAPI 3.1 Nativo** | ⚠️ Requer bibliotecas de terceiros | ✅ **`AddValidationTransformer()`** nativo (.NET 10) |
| **Erros Estruturados** | ❌ Focado em strings genéricas | ✅ `Code`, `Path`, `Severity` e `Arguments` seguros |
| **Localização / i18n** | ⚠️ Resx estático global | ✅ **`IStringLocalizer` / `Accept-Language`** por requisição |
| **Observabilidade** | ❌ Nenhuma métrica nativa | ✅ **OpenTelemetry Meter & Tracing** integrados |
| **Cenários & PATCH** | ⚠️ `RuleSet` rígido | ✅ `ForScenarios("create")` e `ForPaths("Address.City")` |
| **Compatibilidade AOT** | ⚠️ Reflection intensivo | ✅ **100% Native AOT & Trimming-ready** |
| **Execução Concorrente** | ❌ Apenas sequencial | ✅ `ValidationExecutionMode.Parallel` com `Task.WhenAll` |

---

## 📦 Pacotes

| Pacote | Finalidade |
| --- | --- |
| `eQuantic.Validation.Abstractions` | Contratos (`IValidator<T>`, `IValidatable<T>`, `ValidationResult`) e atributos (`[GenerateValidator]`, `[Required]`, `[Email]`, etc.). |
| `eQuantic.Validation` | Motor da DSL fluente, pattern matching, regras assíncronas e composição. |
| `eQuantic.Validation.AspNetCore` | Minimal APIs (`RequireValidation`), MVC Action Filter, OpenAPI transformer, i18n e OpenTelemetry. |
| `eQuantic.Validation.Generator` | Roslyn Source Generator para validação procedural e registro DI zero-reflection. |

---

## 📄 Licença

MIT © eQuantic Tech. Consulte [LICENSE](LICENSE).
