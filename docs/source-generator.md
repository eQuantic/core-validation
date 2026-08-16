# Source generator

`eQuantic.Validation.Generator` is an analyzer package (nothing ships to your output folder). It
does four jobs at compile time.

```xml
<PackageReference Include="eQuantic.Validation.Generator"
                  PrivateAssets="all"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## 1. Generated validators from attributes

```csharp
using eQuantic.Validation.Attributes;

[GenerateValidator]
public sealed record CreateCustomer(
    [Required, NotWhiteSpace] string Name,
    [Required, Email(Code = "customer.email.invalid")] string Email,
    [Range(18, 120)] int Age,
    [MinLength(1), ValidateEach] IReadOnlyList<ContactDto> Contacts,
    [ValidateNested] AddressDto Address);
```

The generator emits `CreateCustomerGeneratedValidator : IValidator<CreateCustomer>` — procedural
code with zero reflection, invariant-culture literals, cached regexes, and full localization
support (templates and arguments are preserved, not baked in). `System.ComponentModel.DataAnnotations`
attributes (`[Required]`, `[EmailAddress]`, `[StringLength]`, `[Range]`, …) are honored too.

Every attribute accepts `Code`, `Message`, `Severity` and `Scenarios`:

```csharp
[Pattern("^VIP-", Scenarios = ["vip"], Code = "customer.vip.prefix")] string? MembershipCode
```

`[ValidateNested]` and `[ValidateEach]` resolve the typed `IValidator<T>` of the nested model
from DI (no `MakeGenericType`), so register the nested validators as well. `[CustomRule]` calls
back into your code:

```csharp
[GenerateValidator]
public sealed record Item([CustomRule("ValidateSku", Code = "item.sku.invalid")] string Sku)
{
    public bool ValidateSku(string? sku) => sku is not null && sku.StartsWith("ITEM-");
}
```

## 2. Compile-time DI registration

```csharp
builder.Services.AddGeneratedValidation();
```

Generated as `internal` per assembly: it registers **only that assembly's validators** (generated
and hand-written) as scoped services. Referenced assemblies are never scanned — no surprise
registrations, no runtime reflection. Compose across assemblies explicitly:

```csharp
services.AddValidator<SharedModel, SharedModelValidator>();
```

## 3. Pre-compiled fluent accessors

For every `Validator<T>` subclass, the generator extracts the member lambdas used by
`RuleFor`/`RuleForEach` and registers pre-compiled delegates at module load. Effects:

- validators are scoped, and without this each request paid `Expression.Compile()` per rule;
- under Native AOT the fluent path skips the expression interpreter entirely.

No code changes needed; unregistered lambdas (private nested models, computed chains) fall back
to the previous behavior.

## 4. Usage diagnostics

| ID | Severity | Example that triggers it | Fix |
| --- | --- | --- | --- |
| `VALGEN001` | Warning | `AddGeneratedValidation()` without the core + DI abstractions packages | reference `eQuantic.Validation` |
| `VALGEN002` | Error | `[CustomRule("Missing")]` — no such `bool` method | declare `bool Missing(string? value)` |
| `VALGEN003` | Warning | `[ValidateEach] string Name` — not `IEnumerable<T>` | apply to collections only |
| `VALGEN004` | Warning | `[Email] int Age`, `[GenerateValidator]` on a generic type | move/remove the rule |
| `VALGEN005` | Error | `RuleFor(x => x.Name!.Trim())` — not a member path | `RuleFor(x => x.Name)` |
| `VALGEN006` | Error | `RuleFor(x => x.Name).WithMessage("...")` — configurator with no rule | add a rule first |
| `VALGEN007` | Warning | sync `Validate` on a validator with unconditional async rules | call `ValidateAsync` |

`VALGEN007` understands scenarios: `.MustAsync(...).ForScenarios("create")` keeps the sync path
legal outside that scenario, so it is not flagged.

## Native AOT

`eQuantic.Validation.Abstractions`, `eQuantic.Validation` and `eQuantic.Validation.Testing` build
with `IsAotCompatible` (trim/AOT analyzers). The repository CI publishes and runs an AOT smoke
app on every build ([test/eQuantic.Validation.AotSmoke](../test/eQuantic.Validation.AotSmoke/Program.cs))
covering generated validators, fluent validators with pre-compiled accessors, manifests and the
Zod exporter. The DataAnnotations adapter `AttributeValidator<T>` is the one reflection-based
component and is annotated with `[RequiresUnreferencedCode]`.
