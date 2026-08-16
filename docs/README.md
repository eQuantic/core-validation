# eQuantic.Validation — Documentation

Learn the library in the order it was designed to be learned: rules first, then the layers that
turn them into contracts. Every code sample in these guides is backed by a test in the repository.

## Guides

| # | Guide | You will learn |
|---|-------|----------------|
| 1 | [Getting started](getting-started.md) | Your first validator three ways (fluent, inline, generated), DI registration and a validated endpoint in ten minutes. |
| 2 | [Fluent rules reference](fluent-rules.md) | Every rule with an example: strings, comparisons, cross-property, pattern matching, Value Objects, child rules, scenarios and conditions. |
| 3 | [Source generator](source-generator.md) | `[GenerateValidator]`, compile-time DI registration, pre-compiled accessors, and the `VALGEN` diagnostics catalog. |
| 4 | [ASP.NET Core](aspnetcore.md) | `RequireValidation`, the MVC filter, the 400 problem shape, warnings on 200 responses, async deduplication and execution modes. |
| 5 | [Localization](localization.md) | Built-in translations (en/pt/es/fr/de/it), your own resources via `IStringLocalizer`, lookup by stable code, per-request culture. |
| 6 | [Rule manifest & export](manifest-and-export.md) | `Describe()`, OpenAPI enrichment from fluent rules, and exporting validators as TypeScript Zod schemas. |
| 7 | [Testing validators](testing.md) | `TestValidate` with chainable assertions over codes, severities and arguments — no message-string matching. |
| 8 | [Observability](observability.md) | OpenTelemetry meters, the activity source, and wiring them into your exporter. |
| 9 | [Migrating from FluentValidation](migrating-from-fluentvalidation.md) | Rule-by-rule equivalences, the behavioral differences, and what you gain. |

## Packages at a glance

```
eQuantic.Validation.Abstractions   contracts: IValidator<T>, ValidationResult, attributes,
    │                              rule descriptors, built-in message translations
    └── eQuantic.Validation        the engine: fluent DSL, DI dispatching, async dedup,
        │                          OpenTelemetry, Zod export
        ├── eQuantic.Validation.AspNetCore   RequireValidation, MVC filter, ProblemDetails,
        │                                    warnings header, OpenAPI transformer, i18n
        └── eQuantic.Validation.Testing      TestValidate + chainable assertions

eQuantic.Validation.Generator      analyzer package (no runtime dependency): generated
                                   validators, compile-time registration, pre-compiled
                                   accessors, VALGEN usage diagnostics
```

Start with guide 1 — it takes ten minutes and everything else builds on it.

Maintainers: the automated release flow (semantic-release, commit types → versions) is described
in [releasing.md](releasing.md).
