# Changelog

## [2.1.0](https://github.com/eQuantic/core-validation/compare/v2.0.0...v2.1.0) (2026-08-16)

### Features

* add eQuantic.Validation.Testing package with chainable assertions ([29487ba](https://github.com/eQuantic/core-validation/commit/29487ba65d4bd43d7f088383fb04e2a86e91f1b1))
* pre-compiled fluent accessors and rule-writing shortcuts ([fdd9dc8](https://github.com/eQuantic/core-validation/commit/fdd9dc8be12ed83a6ad104b1d72685153aac2972))
* rule manifest with Describe(), fluent OpenAPI enrichment and Zod export ([2e1f587](https://github.com/eQuantic/core-validation/commit/2e1f587d702c98d88dbebe6755352d77e39fe730))
* surface validation warnings on successful HTTP responses ([2829b9f](https://github.com/eQuantic/core-validation/commit/2829b9f8dcd9e41b94fe54294bb3fc7ca66f3c2a))
* usage analyzer turns runtime validator mistakes into build errors ([f9a0859](https://github.com/eQuantic/core-validation/commit/f9a08598ae053e0c18bb4e0f8e2c0ddd29d9ba9a))

### Performance Improvements

* deduplicate async rule evaluations within a validation operation ([7452910](https://github.com/eQuantic/core-validation/commit/745291043430cd44551c7fa4685735ba78b6db09))

## [2.0.0](https://github.com/eQuantic/core-validation/compare/v1.0.0...v2.0.0) (2026-08-16)

### ⚠ BREAKING CHANGES

* AddValidation is renamed to AddValidationDispatcher and
moved, with IValidationDispatcher/ValidationDispatcher/OTel instrumentation,
from eQuantic.Validation.AspNetCore into eQuantic.Validation (workers no
longer need ASP.NET Core; avoids clashing with .NET 10's built-in
AddValidation). The MVC integration is now AddValidationFilter.
AddGeneratedValidation is generated internal and only registers the
compiling assembly's validators; referenced assemblies are never scanned.
String rules (Email, MinimumLength, MaximumLength, Length, Matches,
NotWhiteSpace) are extension methods that only bind to string properties.
HTTP validation filters run sequentially by default; Parallel is opt-in.
IValidatable<T> was removed. The validation.duration.ms metric is now
validation.duration in seconds. ProblemDetails extensions.issues is emitted
as JSON nodes for source-generated serialization compatibility.

### Features

* harden source generator, move DI dispatching into core, typed string rules ([badb372](https://github.com/eQuantic/core-validation/commit/badb372de84a447a52a5e2ca563c94ed5fca697b))

## 1.0.0 (2026-08-16)

### Features

* initial implementation of eQuantic.Validation library, source generator and aspnetcore integration ([a436462](https://github.com/eQuantic/core-validation/commit/a436462351840cb6e1a96553e314d26b285b603f))

## 0.1.0

- First public foundation of the eQuantic.Validation family.
- Framework-independent fluent validation engine with async rules, scenarios, composition and structured issues.
- Optional ASP.NET Core integration for Minimal APIs and MVC controllers.
- Incremental source generator for compile-time DI registration without assembly scanning.
