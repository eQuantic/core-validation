# eQuantic.Validation.Generator

Incremental Roslyn source generator and usage analyzer for eQuantic.Validation. Trimming- and Native AOT-friendly: no reflection, no `MakeGenericType`, no runtime expression compilation.

```xml
<PackageReference Include="eQuantic.Validation.Generator"
                  PrivateAssets="all"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Install it in every project that declares models with `[GenerateValidator]` or fluent validators.

### What it generates

**1. Reflection-free validators** for annotated models:

```csharp
[GenerateValidator]
public sealed record CreateCustomer(
    [Required, NotWhiteSpace] string Name,
    [Required, Email(Code = "customer.email.invalid")] string Email,
    [Range(18, 120)] int Age);
// → CreateCustomerGeneratedValidator : IValidator<CreateCustomer>, IDescribableValidator
```

**2. Compile-time DI registration** — `AddGeneratedValidation()` is generated `internal` to the
compiling assembly and registers only that assembly's validators as scoped services. Referenced
assemblies are never scanned; compose them explicitly with `AddValidator<TModel, TValidator>()`.

```csharp
builder.Services.AddGeneratedValidation();
```

**3. Pre-compiled fluent accessors** — every `RuleFor(x => x.Prop)` lambda in your `Validator<T>`
subclasses is compiled at build time and registered at module load. The fluent path never pays
`Expression.Compile()` at runtime (validators are scoped, so that cost used to repeat per request)
and never runs through the expression interpreter under Native AOT.

### Compile-time diagnostics

| ID | Severity | Example that triggers it | Fix |
| --- | --- | --- | --- |
| `VALGEN001` | Warning | `AddGeneratedValidation()` without the core + DI abstractions packages | reference `eQuantic.Validation` |
| `VALGEN002` | Error | `[CustomRule("Missing")]` — no such `bool` method | declare `bool Missing(string? value)` on the model |
| `VALGEN003` | Warning | `[ValidateEach] string Name` — not `IEnumerable<T>` | apply to collections only |
| `VALGEN004` | Warning | `[Email] int Age` — string rule on non-string | move the rule to a string property |
| `VALGEN005` | Error | `RuleFor(x => x.Name!.Trim())` — not a member path | `RuleFor(x => x.Name)` and normalize elsewhere |
| `VALGEN006` | Error | `RuleFor(x => x.Name).WithMessage("...")` — configurator with no rule | add a rule first: `.NotWhiteSpace().WithMessage("...")` |
| `VALGEN007` | Warning | `validator.Validate(m)` where the validator has unconditional `MustAsync` | call `ValidateAsync` |

`VALGEN007` is scenario-aware: async rules scoped with `.ForScenarios("create")` don't flag
synchronous validation.
