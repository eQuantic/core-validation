# Migrating from FluentValidation

This guide maps every common FluentValidation construct to its eQuantic.Validation equivalent,
calls out the behavioral differences you must know, and shows what you gain after the move.
Most validators migrate mechanically; a medium codebase is typically done in under an hour.

## Packages

| FluentValidation | eQuantic.Validation |
| --- | --- |
| `FluentValidation` | `eQuantic.Validation` |
| `FluentValidation.AspNetCore` (**deprecated**) | `eQuantic.Validation.AspNetCore` (first-class, maintained) |
| `FluentValidation.DependencyInjectionExtensions` | built into `eQuantic.Validation` (`AddValidator`) + `eQuantic.Validation.Generator` (`AddGeneratedValidation`) |
| `FluentValidation.TestHelper` | `eQuantic.Validation.Testing` |

## Validators

```csharp
// FluentValidation                          // eQuantic.Validation
public class CustomerValidator               public sealed class CustomerValidator
    : AbstractValidator<Customer>                : Validator<Customer>
{                                            {
    public CustomerValidator()                   public CustomerValidator()
    {                                            {
        RuleFor(x => x.Email)                        RuleFor(x => x.Email)
            .NotEmpty()                                  .NotWhiteSpace()
            .EmailAddress();                             .Email();
    }                                            }
}                                            }
```

No subclass needed for quick cases:

```csharp
var validator = new InlineValidator<Customer>(v => v.RuleFor(x => x.Email).Email());
```

## Rule-by-rule equivalences

| FluentValidation | eQuantic.Validation | Notes |
| --- | --- | --- |
| `NotNull()` | `NotNull()` | identical |
| `NotEmpty()` | `NotEmpty()` / `NotWhiteSpace()` | `NotWhiteSpace` also rejects whitespace-only strings |
| `EmailAddress()` | `Email()` | string-only at compile time |
| `MinimumLength(n)` / `MaximumLength(n)` / `Length(a, b)` | same names | string-only at compile time |
| `Matches(pattern)` | `Matches(pattern)` | pattern flows into the manifest / OpenAPI / Zod |
| `GreaterThan(value)` / `LessThan` / `GreaterThanOrEqualTo` / `LessThanOrEqualTo` / `InclusiveBetween` | same names | plus `OneOf(...)`, `NotEqualTo(...)` |
| `GreaterThan(x => x.Other)` (cross-property) | `GreaterThan(x => x.Other)` | identical shape; also `EqualTo(x => x.Password)` for confirmations |
| `Equal(value)` / `NotEqual(value)` | `EqualTo(value)` / `NotEqualTo(value)` | |
| `Must(v => ...)` | `Must(v => ..., code: "my.code")` | attach a stable error code |
| `MustAsync((v, ct) => ...)` | `MustAsync((model, v, ctx, ct) => ..., "my.code")` | model and context are handed to you |
| `SetValidator(new ChildValidator())` | `SetValidator(new ChildValidator())` | identical |
| `RuleForEach(x => x.Items).SetValidator(...)` | identical | element paths render as `Items[0].Name` |
| `ChildRules(c => ...)` | `ChildRules(c => ...)` | identical shape on members and collections |
| `When(cond)` | `When(cond)` | rule-scoped |
| `WithMessage` / `WithName` / `WithSeverity` | same names | plus `WithCode` for stable machine-readable codes |
| `RuleSet("Create", ...)` | `.ForScenarios("create")` per rule | run with `ValidationContext.ForScenarios("create")` or `RequireValidation("create")` |
| `CascadeMode.Stop` | `StopOnFirstFailure()` | per rule chain |
| `Include(other)` | `Include(other)` | identical |
| `validator.TestValidate(m).ShouldHaveValidationErrorFor(x => x.Email)` | `validator.TestValidate(m).ShouldHaveFailureFor(x => x.Email)` | refine with `.WithCode`, `.WithSeverity`, `.WithArgument` — no message-string matching |

## Registration

```csharp
// FluentValidation
services.AddValidatorsFromAssemblyContaining<CustomerValidator>();   // runtime assembly scanning

// eQuantic.Validation — explicit, or compile-time via the generator (no scanning, AOT-safe):
services.AddValidator<Customer, CustomerValidator>();
services.AddGeneratedValidation();   // registers every validator declared in this assembly
```

ASP.NET Core auto-validation (removed from FluentValidation) is first-class here:

```csharp
app.MapPost("/customers", (CreateCustomer c) => Results.Created()).RequireValidation();
builder.Services.AddControllers().AddValidationFilter();   // MVC
```

## Behavioral differences to review during migration

1. **Null is permissive.** Shape rules (`Email`, `MinimumLength`, `GreaterThan`, …) pass on
   `null`; require presence explicitly with `NotNull()`/`NotEmpty()`/`NotWhiteSpace()`.
   FluentValidation mixes both behaviors per rule — here it is uniform. Exception:
   `EqualTo(x => x.Other)` is strict, so confirmation fields must actually match.
2. **Sync vs async is explicit.** Calling `Validate` on a validator with unconditional async
   rules throws `AsyncValidationRequiredException` (and the analyzer flags it at build time as
   `VALGEN007`) instead of silently blocking or skipping.
3. **Rules run sequentially by default** in the HTTP filters; `ValidationExecutionMode.Parallel`
   is opt-in and requires concurrency-safe dependencies (a scoped EF Core `DbContext` is not).
4. **Messages are structured.** Prefer asserting and branching on `Code` instead of message
   text; templates stay localizable (`AddValidationLocalization()` ships en/pt/es/fr/de/it).

## What you gain after migrating

- Stable error **codes**, **severities** (warnings reach the client on 200 via the
  `Validation-Warnings` header) and structured **arguments** on every failure.
- **Rules as data**: `Describe()` manifests, OpenAPI enrichment from fluent rules, and
  `ZodSchemaExporter` for client-side schemas.
- **Source generator**: reflection-free validators from attributes, compile-time DI
  registration, pre-compiled fluent accessors (no `Expression.Compile()` per request), and
  usage diagnostics (`VALGEN001`–`VALGEN007`).
- **Async I/O deduplication** per request, OpenTelemetry metrics and tracing, verified
  trimming/Native AOT compatibility — and 2.9x/4.5x measured speedups over FluentValidation.
