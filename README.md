# eQuantic.Validation

[![CI](https://github.com/equantic/core-validation/actions/workflows/ci.yml/badge.svg)](https://github.com/equantic/core-validation/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/eQuantic.Validation.svg)](https://www.nuget.org/packages/eQuantic.Validation/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Modern, high-performance validation for .NET 10: Compile-time Source Generated validators (Zero-Allocation), Relational Pattern Matching, Native OpenAPI 3.1 schema transformers, OpenTelemetry metrics and distributed tracing, per-request i18n, and full Native AOT compatibility.**

---

## ⚡ Three Innovative Ways to Validate:

### 1. Declarative Validation with Roslyn Source Generator (Zero-Allocation & Native AOT)

Eliminates the need for hand-crafted validator classes on simple DTOs and Commands. The incremental Roslyn Source Generator emits procedural validation code at compile time — **zero reflection, zero runtime expression trees, and zero boxing**:

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

### 2. Code-First Fluent Validation with Relational Pattern Matching

Express complex conditional, relational, and cross-field rules cleanly using modern C# Pattern Matching syntax:

```csharp
public sealed class PaymentValidator : Validator<PaymentRequest>
{
    public PaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);

        // Modern Relational Pattern Matching:
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

### 3. Domain Value Objects (DDD) & Parsable Types Validation

Validate domain Value Objects creation and parsable types without coupling your validation layer to external domain frameworks:

```csharp
public sealed class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        // Value Object factory (fails safely if factory throws or returns null):
        RuleFor(x => x.Email)
            .MustCreate(
                email => EmailVo.Create(email),
                code: "customer.email.invalid",
                messageTemplate: "Invalid email format.");

        // Native .NET IParsable (Guid, DateOnly, Money, IPAddress, etc.):
        RuleFor(x => x.TransactionId)
            .MustParse<Guid>(
                code: "customer.transaction_id.invalid",
                messageTemplate: "Transaction ID must be a valid GUID.");
    }
}
```

### 4. Asynchronous I/O Rules with Dependency Injection

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

## 🚀 ASP.NET Core & Minimal APIs Integration (.NET 10)

```csharp
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Generated;

var builder = WebApplication.CreateBuilder(args);

// 1. Compile-time generated Dependency Injection registration (Zero Assembly Scanning)
builder.Services.AddGeneratedValidation();

// 2. Automatic localization / i18n (Accept-Language header)
builder.Services.AddValidationLocalization<ValidationResources>();

// 3. Native OpenAPI 3.1 schema transformer (Scalar / Swagger)
builder.Services.AddOpenApi(options =>
{
    options.AddValidationTransformer();
});

var app = builder.Build();

app.MapPost("/customers", (CreateCustomer command) => Results.Created())
   .RequireValidation("create");
```

---

## 📊 Observability & Metrics with OpenTelemetry

Native zero-overhead metrics (`System.Diagnostics.Metrics`) and distributed tracing (`ActivitySource`) with **Zero PII**:

- `validation.requests.total`: Counter for validation requests partitioned by model type and outcome.
- `validation.failures.total`: Breakdown of validation failures by stable error code.
- `validation.duration.ms`: Latency histogram in milliseconds.

---

## 🏎️ Scientific Benchmarks (BenchmarkDotNet on .NET 10)

Official results measured with `BenchmarkDotNet v0.14.0` on **.NET 10.0** (Apple M4 Pro Arm64):

| Method / Approach | Scenario | Mean Latency (`Mean`) | Memory Allocated (`Allocated`) | Performance vs FluentValidation |
| :--- | :---: | :---:| :---: | :---: |
| 🥇 **eQuantic (Source Generated)** | **Valid** | **87.22 ns** | **32 B** | 🚀 **2.7x faster / 95% less memory** |
| 🥈 **eQuantic (Fluent DSL)** | **Valid** | **136.85 ns** | **160 B** | ⚡ **1.7x faster / 77% less memory** |
| 🥉 **FluentValidation** | **Valid** | **235.36 ns** | **696 B** | *Baseline* |
| | | | | |
| 🥇 **eQuantic (Source Generated)** | **Invalid (Failures)** | **175.89 ns** | **1,168 B** | 🚀 **16.5x faster / 91% less memory** |
| 🥈 **eQuantic (Fluent DSL)** | **Invalid (Failures)** | **1.108 µs** | **5,536 B** | ⚡ **2.6x faster / 56% less memory** |
| 🥉 **FluentValidation** | **Invalid (Failures)** | **2.899 µs** | **12,600 B** | *Baseline* |

> *To reproduce benchmarks on your machine, run: `dotnet run -c Release --project benchmarks/eQuantic.Validation.Benchmarks`.*

---

## 🌟 Feature Comparison: eQuantic.Validation vs. FluentValidation

| Feature | Traditional FluentValidation | eQuantic.Validation |
| --- | --- | --- |
| **Code Generation (Build-time)** | ❌ Runtime only | ✅ **Zero-Allocation Source Generator** via `[GenerateValidator]` |
| **Relational Pattern Matching** | ❌ Nested & verbose `.When(...)` | ✅ Idiomatic **`RuleForModel().Match(p => p is { ... })`** |
| **Native OpenAPI 3.1** | ⚠️ Requires 3rd party libraries | ✅ Native **`AddValidationTransformer()`** (.NET 10) |
| **Structured Errors** | ❌ Primarily plain strings | ✅ Strongly-typed `Code`, `Path`, `Severity`, and `Arguments` |
| **Localization / i18n** | ⚠️ Global static resx | ✅ Per-request **`IStringLocalizer` / `Accept-Language`** |
| **Observability** | ❌ No native metrics | ✅ Integrated **OpenTelemetry Meter & ActivitySource** |
| **Scenarios & PATCH** | ⚠️ Rigid `RuleSet` | ✅ Flexible `ForScenarios("create")` & `ForPaths("Address.City")` |
| **Native AOT & Trimming** | ⚠️ Heavy reflection | ✅ **100% Native AOT & Trimming Ready** |
| **Concurrent Execution** | ❌ Sequential only | ✅ `ValidationExecutionMode.Parallel` via `Task.WhenAll` |

---

## 📦 Packages

| Package | Purpose |
| --- | --- |
| `eQuantic.Validation.Abstractions` | Clean contracts (`IValidator<T>`, `IValidatable<T>`, `ValidationResult`) and declarative attributes (`[GenerateValidator]`, `[Required]`, `[Email]`, etc.). |
| `eQuantic.Validation` | Fluent DSL engine, pattern matching, asynchronous rules, Value Objects, and composition. |
| `eQuantic.Validation.AspNetCore` | Minimal APIs (`RequireValidation`), MVC Action Filter, OpenAPI transformer, i18n, and OpenTelemetry. |
| `eQuantic.Validation.Generator` | Roslyn Source Generator for procedural zero-reflection validation and compile-time DI registration. |

---

## 📄 License

MIT © eQuantic Tech. See [LICENSE](LICENSE).
