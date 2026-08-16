# Changelog

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
