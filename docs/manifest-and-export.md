# Rule manifest & export

Every validator — fluent or source-generated — implements `IDescribableValidator`: its rules
exist as **data**, not just behavior. That single capability powers OpenAPI enrichment, error
catalogs and client-side schema generation.

## Describe()

```csharp
ValidatorDescription manifest = new CreateCustomerValidator().Describe();

foreach (ValidationRuleDescriptor rule in manifest.Rules)
{
    // rule.Path          "Email", "Address.PostalCode", "Contacts[].Email", "" (model-level)
    // rule.Kind          "email", "minimum_length", ... (stable, see ValidationRuleKinds)
    // rule.Code          "customer.email.invalid" (stable error code)
    // rule.Arguments     { MinimumLength = 2 } / { Pattern = "^[0-9]{5}$" } / { OtherPath = "Start" }
    // rule.ValueType     typeof(string), typeof(decimal), ...
    // rule.Scenarios     ["create"] — empty means always
    // rule.IsAsync       needs ValidateAsync
    // rule.IsConditional guarded by When(...)
}
```

Nested and collection validators expand with prefixed paths; opaque rules (`Must`, `MustAsync`,
`Match`, Value Objects) appear with kind `predicate`/`match`/`create`/`parse` and their codes —
never silently dropped. An application-wide catalog is one LINQ statement over the validators you
register, ready for living error-code documentation and contract tests.

## OpenAPI enrichment

```csharp
builder.Services.AddOpenApi(options => options.AddValidationTransformer());
```

The transformer merges declarative attributes **and** every registered describable validator into
the schemas:

| Rule | OpenAPI |
| --- | --- |
| `NotNull` / `NotEmpty` / `NotWhiteSpace` / `[Required]` | `required` |
| `MinimumLength` / `MaximumLength` / `Length` | `minLength` / `maxLength` |
| `Matches` / `[Pattern]` | `pattern` |
| `Email` | `format: email` |
| `GreaterThan` / `LessThan` | `exclusiveMinimum` / `exclusiveMaximum` |
| `GreaterThanOrEqualTo` / `LessThanOrEqualTo` / `InclusiveBetween` / `[Range]` | `minimum` / `maximum` |

Only unconditional, scenario-free, blocking rules on top-level members map into the schema —
cross-property comparisons and predicates stay server-side by design.

## Zod / TypeScript export

Write validation once in C#, enforce it in the browser:

```csharp
using eQuantic.Validation.Export;

string ts = ZodSchemaExporter.Export(new CheckoutValidator());
```

```ts
// Generated from Checkout rules by eQuantic.Validation — do not edit.
import { z } from "zod";

export const checkoutSchema = z.object({
  customerName: z.string().min(1).min(2).max(60),
  email: z.string().min(1).email() /* server-side: checkout.email.taken */,
  amount: z.number().gt(0),
  installments: z.number().int().gte(1).lte(12),
  address: z.object({
    street: z.string().min(1),
  }).optional(),
  items: z.array(z.object({
    sku: z.string().min(1),
  })),
});

export type Checkout = z.infer<typeof checkoutSchema>;
```

What the exporter guarantees:

- **Nothing is silently dropped**: server-only rules (async uniqueness, predicates, cross-property
  comparisons) become `/* server-side: code */` comments, so the front-end team sees them.
- **Optionality is faithful**: members without a presence rule are `.optional()`, except
  non-nullable value types (a JSON number is always there).
- **Scenarios are respected**: excluded by default, exportable per scenario:

```csharp
var createSchema = ZodSchemaExporter.Export(validator, new ZodExportOptions { Scenario = "create" });
```

Wire it into your build (a console tool, an MSBuild target or a test that writes the file) and the
front-end schema can never drift from the backend rules again.
