# eQuantic.Validation

[![CI](https://github.com/eQuantic/core-validation/actions/workflows/ci.yml/badge.svg)](https://github.com/eQuantic/core-validation/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/eQuantic.Validation.svg)](https://www.nuget.org/packages/eQuantic.Validation/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Modern, high-performance validation for .NET 10: compile-time source-generated validators (low-allocation, reflection-free), relational pattern matching, native OpenAPI 3.1 schema transformers, OpenTelemetry metrics and tracing, per-request i18n, and Native AOT compatibility proven by a CI-published native binary.**

📚 **[Documentation](docs/README.md)** — nine guides with runnable examples, from getting started to Zod export. Coming from FluentValidation? [Start here](docs/migrating-from-fluentvalidation.md).

---

## ⚡ Three Ways to Validate

### 1. Declarative Validation with a Roslyn Source Generator (Reflection-Free)

Eliminates hand-crafted validator classes on simple DTOs and Commands. The incremental Roslyn source generator emits procedural validation code at compile time — no reflection, no runtime expression trees:

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

Nested (`[ValidateNested]`) and collection (`[ValidateEach]`) validators resolve through the
typed `IValidator<T>` service registered in DI — the generator knows the element type at compile
time, so no `MakeGenericType` and no runtime reflection are involved.

### 2. Code-First Fluent Validation with Relational Pattern Matching

Express complex conditional, relational, and cross-field rules cleanly using modern C# pattern matching. String rules (`Email`, `MinimumLength`, `Matches`, …) only bind to `string` properties, so mistakes fail at compile time instead of at runtime:

```csharp
public sealed class PaymentValidator : Validator<PaymentRequest>
{
    public PaymentValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);

        // Modern relational pattern matching:
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

Shortcuts cover the common cases without ceremony — `GreaterThanOrEqualTo`, `LessThanOrEqualTo`,
`NotEqualTo`, `OneOf`, cross-property comparisons, inline child rules and `InlineValidator<T>`
for rules without a subclass:

```csharp
var validator = new InlineValidator<Order>(v =>
{
    v.RuleFor(x => x.Quantity).GreaterThanOrEqualTo(1);
    v.RuleFor(x => x.Currency).NotNull().OneOf("BRL", "EUR", "USD");
    v.RuleFor(x => x.Status).NotEqualTo("deleted");

    // Compare against other members of the same model:
    v.RuleFor(x => x.DeliveryDate).GreaterThan(x => x.OrderDate);
    v.RuleFor(x => x.ConfirmEmail).EqualTo(x => x.Email);

    // Child rules without a separate validator class:
    v.RuleForEach(x => x.Items).ChildRules(item =>
    {
        item.RuleFor(i => i.Sku).NotWhiteSpace();
        item.RuleFor(i => i.Quantity).GreaterThan(0);
    });
});
```

Migrating from FluentValidation? The rule-by-rule mapping lives in
[docs/migrating-from-fluentvalidation.md](docs/migrating-from-fluentvalidation.md).

When the source generator is installed, every `RuleFor(x => x.Prop)` accessor is pre-compiled at
build time and registered at module load — the fluent path never pays `Expression.Compile()` at
runtime (notably: validators are scoped, so without this the compilation cost repeats per request).

### 3. Domain Value Objects (DDD) & Parsable Types Validation

Validate domain Value Object creation and parsable types without coupling your validation layer to external domain frameworks:

```csharp
public sealed class CreateCustomerValidator : Validator<CreateCustomerRequest>
{
    public CreateCustomerValidator()
    {
        // Value Object factory (fails safely if the factory throws or returns null):
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

### Asynchronous I/O Rules with Dependency Injection

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

Calling `Validate` on a validator with active asynchronous rules throws
`AsyncValidationRequiredException` — async work is never silently skipped or blocked on.

Async rules are **deduplicated per operation**: within one request (the HTTP integrations and the
dispatcher enable this automatically), the same rule for the same instance and value runs once —
composed validators, repeated collection elements and revalidation reuse the first result instead
of repeating the I/O. Standalone usage opts in with
`new ValidationContext(deduplicateAsyncRules: true)`; nested validators share the cache.

---

## 🚀 ASP.NET Core & Minimal APIs Integration (.NET 10)

```csharp
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Generated;

var builder = WebApplication.CreateBuilder(args);

// 1. Compile-time generated DI registration (no assembly scanning).
//    AddGeneratedValidation is generated *internal* to each assembly: referencing a library
//    never registers its validators behind your back — each assembly opts in explicitly.
builder.Services.AddGeneratedValidation();

// 2. Localization / i18n. The parameterless overload ships built-in translations for the
//    default messages (en, pt, es, fr, de, it); pass a resource type to use your own resx.
//    Combine with UseRequestLocalization so Accept-Language drives the culture per request.
builder.Services.AddValidationLocalization();                          // built-in languages
// builder.Services.AddValidationLocalization<ValidationResources>();  // your own resources

// 3. Native OpenAPI 3.1 schema transformer (Scalar / Swagger)
builder.Services.AddOpenApi(options =>
{
    options.AddValidationTransformer();
});

var app = builder.Build();

app.MapPost("/customers", (CreateCustomer command) => Results.Created())
   .RequireValidation("create");
```

Validators registered manually (from any assembly) compose with the generated ones:

```csharp
builder.Services.AddValidator<CreateOrder, CreateOrderValidator>();
```

Rules run **sequentially by default**. Parallel execution is opt-in per endpoint — use it only
when every async rule dependency is safe for concurrent use (a scoped EF Core `DbContext` is not):

```csharp
app.MapPost("/quotes", handler)
   .RequireValidation(ValidationExecutionMode.Parallel);
```

> **Why `AddValidationDispatcher` and not `AddValidation`?** .NET 10 ships its own
> `IServiceCollection.AddValidation()` (Microsoft.Extensions.Validation). The name describes
> exactly what is registered and avoids extension-method ambiguity when both are in scope.
> You rarely call it directly — `AddValidator` and `AddGeneratedValidation` call it for you.

---

## 🆚 eQuantic.Validation vs. .NET 10 Built-in Validation

.NET 10 introduced built-in validation for Minimal APIs (`Microsoft.Extensions.Validation`,
`AddValidation()` + `[ValidatableType]`), which source-generates DataAnnotations checks. Reach for
eQuantic.Validation when you need what it doesn't cover:

| Capability | .NET 10 built-in | eQuantic.Validation |
| --- | --- | --- |
| DataAnnotations attributes | ✅ | ✅ (same attributes, plus `[NotWhiteSpace]`, `[GreaterThan]`, `[CustomRule]`, …) |
| Structured errors (stable `Code`, `Severity`, `Arguments`) | ❌ plain strings | ✅ |
| Fluent / cross-field / pattern-matching rules | ❌ | ✅ |
| Async rules with DI (uniqueness checks, lookups) | ❌ | ✅ |
| Scenarios (`create`/`update`) & partial validation (`ForPaths`, PATCH) | ❌ | ✅ |
| Value Objects & `IParsable` | ❌ | ✅ |
| OpenTelemetry metrics & tracing | ❌ | ✅ |
| Localization by stable error code | ❌ | ✅ |

---

## 🧪 Testing Validators

`eQuantic.Validation.Testing` ships `TestValidate` with chainable, framework-agnostic assertions
over the structured failure model — tests match on stable codes and rule arguments, never on
message strings:

```csharp
using eQuantic.Validation.Testing;

validator.TestValidate(new CreateCustomer(Name: "A", Email: "nope", Age: 15))
    .ShouldBeInvalid()
    .ShouldHaveFailureFor(x => x.Email).WithCode("customer.email.invalid");

validator.TestValidate(valid).ShouldBeValid().ShouldNotHaveFailureFor(x => x.Email);

var result = await validator.TestValidateAsync(customer, "create");   // scenarios + async rules
result.ShouldHaveFailureWithCode("customer.email.taken");

validator.TestValidate(tooShort)
    .ShouldHaveFailureFor("Name").WithArgument("MinimumLength", 2).Exactly(1);
```

---

## ⚠️ Warnings That Reach the Client

`Severity.Warning` rules don't block the request — and they don't die on the server either. When
a valid request carries warnings, the HTTP integrations attach them to the successful response
automatically:

```csharp
public sealed class ProfileValidator : Validator<UpdateProfile>
{
    public ProfileValidator()
    {
        RuleFor(x => x.Nickname)
            .Must(n => n != "legacy", "profile.nickname.legacy")
            .WithMessage("This nickname format is deprecated.")
            .WithSeverity(ValidationSeverity.Warning);   // valid, but tell the caller
    }
}
```

```http
HTTP/1.1 200 OK
Validation-Warnings: Nickname:profile.nickname.legacy
```

The header carries compact `path:code` pairs; handlers that want to enrich the response body get
the full failures from `ValidationWarnings.GetWarnings(httpContext)`:

```csharp
app.MapPut("/profile", (UpdateProfile request, HttpContext http) =>
{
    var warnings = ValidationWarnings.GetWarnings(http);   // full path/code/message/arguments
    return Results.Ok(new { saved = true, warnings });
}).RequireValidation();
```

---

## 🧭 Compile-Time Diagnostics

The generator package also ships a usage analyzer: mistakes that would only explode at runtime
become build-time diagnostics with the offending code highlighted.

| ID | Severity | Triggers on | Instead of |
| --- | --- | --- | --- |
| `VALGEN001` | Warning | `AddGeneratedValidation()` without `eQuantic.Validation` + `M.E.DependencyInjection.Abstractions` referenced | registrations silently missing |
| `VALGEN002` | Error | `[CustomRule("Missing")]` naming a method that doesn't exist or doesn't return `bool` | cryptic errors inside generated code |
| `VALGEN003` | Warning | `[ValidateEach]` on a property that isn't `IEnumerable<T>` | rule silently skipped |
| `VALGEN004` | Warning | rule/type mismatch, e.g. `[Email] int Age` or `[GenerateValidator]` on a generic type | runtime surprises |
| `VALGEN005` | Error | `RuleFor(x => x.Name!.Trim())` — anything but a member path | `ArgumentException` at runtime |
| `VALGEN006` | Error | `RuleFor(x => x.Name).WithMessage(...)` with no rule before the configurator | `InvalidOperationException` when the validator is constructed |
| `VALGEN007` | Warning | `validator.Validate(model)` on a validator declaring unconditional async rules | `AsyncValidationRequiredException` at runtime |

`VALGEN007` understands scenarios: an async rule scoped with `.ForScenarios("create")` doesn't
flag synchronous validation, because outside that scenario the sync path is legal.

---

## 📜 Rule Manifest: Validation as Data

Every validator — fluent or source-generated — implements `IDescribableValidator` and publishes
its rules as data: path, kind, stable error code, parameters, scenarios, severity and value type.
Aggregate the registered validators and you have the expandable rule catalog of the whole
application, queryable at runtime or exportable at build time:

```csharp
ValidatorDescription manifest = new CreateCustomerValidator().Describe();
// → [ { Path: "Email", Kind: "email", Code: "customer.email.invalid", ... },
//     { Path: "Address.PostalCode", Kind: "pattern", Arguments: { Pattern: "^[0-9]{5}$" } },
//     { Path: "Contacts[].Email", Kind: "email", ... } ]
```

The manifest powers three things out of the box:

1. **OpenAPI**: `AddValidationTransformer()` enriches schemas from attributes *and* from every
   registered describable validator — fluent rules like `MinimumLength`, `Matches` and
   `GreaterThan` land in the contract as `minLength`, `pattern` and `exclusiveMinimum`.
2. **Error-code catalog**: stable codes with their paths and parameters, ready for living API
   documentation and contract tests.
3. **Client-side schemas**: export validators as TypeScript Zod schemas — validation written once
   in C#, enforced in the browser. Opaque server-side rules (async uniqueness checks, custom
   predicates) are surfaced as comments with their error codes, never silently dropped:

```csharp
string ts = ZodSchemaExporter.Export(new CheckoutValidator());
// export const checkoutSchema = z.object({
//   customerName: z.string().min(1).min(2).max(60),
//   email: z.string().min(1).email() /* server-side: checkout.email.taken */,
//   amount: z.number().gt(0),
//   address: z.object({ street: z.string().min(1) }).optional(),
//   items: z.array(z.object({ sku: z.string().min(1) })),
// });
// export type Checkout = z.infer<typeof checkoutSchema>;
```

Scenario-scoped rules are excluded by default and can be exported per scenario
(`new ZodExportOptions { Scenario = "create" }`).

---

## 📊 Observability & Metrics with OpenTelemetry

Native metrics (`System.Diagnostics.Metrics`) and distributed tracing (`ActivitySource`) with **zero PII**:

- `validation.requests.total`: validation requests partitioned by model type and outcome.
- `validation.failures.total`: failures broken down by stable error code and severity.
- `validation.duration`: latency histogram in seconds (OTel convention).

---

## 🏎️ Benchmarks (BenchmarkDotNet on .NET 10)

Measured with `BenchmarkDotNet v0.14.0` on **.NET 10.0** (Apple M4 Pro Arm64), package version 2.0.0.
The generated validator allocates a single result list on the happy path — low-allocation, not
literally zero. Failure-path numbers include full runtime message formatting (template +
arguments), which keeps localization consistent across the generated and fluent paths:

| Method / Approach | Scenario | Mean Latency | Allocated | vs FluentValidation |
| :--- | :---: | :---:| :---: | :---: |
| 🥇 **eQuantic (Source Generated)** | **Valid** | **82.94 ns** | **32 B** | 🚀 **2.9x faster / 95% less memory** |
| 🥈 **eQuantic (Fluent DSL)** | **Valid** | **129.27 ns** | **160 B** | ⚡ **1.9x faster / 77% less memory** |
| 🥉 **FluentValidation** | **Valid** | **241.57 ns** | **696 B** | *Baseline* |
| | | | | |
| 🥇 **eQuantic (Source Generated)** | **Invalid (Failures)** | **656.2 ns** | **4,075 B** | 🚀 **4.5x faster / 68% less memory** |
| 🥈 **eQuantic (Fluent DSL)** | **Invalid (Failures)** | **864.1 ns** | **4,079 B** | ⚡ **3.4x faster / 68% less memory** |
| 🥉 **FluentValidation** | **Invalid (Failures)** | **2.973 µs** | **12,600 B** | *Baseline* |

> Reproduce locally with: `dotnet run -c Release --project benchmarks/eQuantic.Validation.Benchmarks`.

---

## 🌟 Feature Comparison: eQuantic.Validation vs. FluentValidation

| Feature | Traditional FluentValidation | eQuantic.Validation |
| --- | --- | --- |
| **Code generation (build-time)** | ❌ Runtime only | ✅ Reflection-free source generator via `[GenerateValidator]` |
| **Relational pattern matching** | ❌ Nested & verbose `.When(...)` | ✅ Idiomatic **`RuleForModel().Match(p => p is { ... })`** |
| **Native OpenAPI 3.1** | ⚠️ Requires 3rd party libraries | ✅ Native **`AddValidationTransformer()`** (.NET 10) |
| **Structured errors** | ❌ Primarily plain strings | ✅ Strongly-typed `Code`, `Path`, `Severity`, and `Arguments` |
| **String rules type-safety** | ✅ Compile-time via `IRuleBuilder<T, string>` | ✅ Compile-time via covariant `IRuleBuilder<T, out TProperty>` |
| **Localization / i18n** | ⚠️ Global static resx | ✅ Built-in translations (en/pt/es/fr/de/it) + per-request **`IStringLocalizer`** with lookup by stable code |
| **Observability** | ❌ No native metrics | ✅ Integrated **OpenTelemetry Meter & ActivitySource** |
| **Scenarios & PATCH** | ⚠️ Rigid `RuleSet` | ✅ Flexible `ForScenarios("create")` & `ForPaths("Address.City")` |
| **Rules as data / client export** | ❌ Opaque runtime lambdas | ✅ `Describe()` manifest + **Zod/TypeScript export** |
| **Trimming / Native AOT** | ⚠️ Heavy reflection | ✅ Trim/AOT analyzers enabled on core packages; generated path is reflection-free¹ |
| **Concurrent execution** | ❌ Sequential only | ✅ Opt-in `ValidationExecutionMode.Parallel` (sequential by default) |

¹ With the generator installed, fluent `RuleFor` accessors are pre-compiled at build time and
registered via module initializer — no `Expression.Compile()` per validator instantiation on JIT
and no expression interpreter under Native AOT. Without the generator, the fluent path falls back
to compiling expressions (interpreted under AOT). `AttributeValidator<T>` (the DataAnnotations
adapter) is reflection-based and annotated with `[RequiresUnreferencedCode]`.

---

## 📦 Packages

| Package | Purpose |
| --- | --- |
| `eQuantic.Validation.Abstractions` | Clean contracts (`IValidator<T>`, `ValidationResult`, `ValidationMessages`) and declarative attributes (`[GenerateValidator]`, `[Required]`, `[Email]`, etc.). |
| `eQuantic.Validation` | Fluent DSL engine, pattern matching, async rules, Value Objects, DI dispatching (`AddValidationDispatcher`, `AddValidator`) and OpenTelemetry instrumentation. |
| `eQuantic.Validation.AspNetCore` | Minimal APIs (`RequireValidation`), MVC action filter (`AddValidationFilter`), OpenAPI transformer and i18n message provider. |
| `eQuantic.Validation.Generator` | Roslyn incremental source generator for reflection-free validators and compile-time DI registration. |
| `eQuantic.Validation.Testing` | `TestValidate` with chainable, framework-agnostic assertions (`ShouldHaveFailureFor`, `WithCode`, `WithArgument`). |

---

## 📄 License

MIT © eQuantic Tech. See [LICENSE](LICENSE).
