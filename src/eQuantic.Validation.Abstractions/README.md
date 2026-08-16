# eQuantic.Validation.Abstractions

Clean, framework-independent validation contracts and declarative attributes for the eQuantic.Validation ecosystem.

```bash
dotnet add package eQuantic.Validation.Abstractions
```

This package defines `IValidator<T>`, `ValidationContext`, `ValidationResult`, `ValidationFailure`, `ValidationSeverity`, `ValidationMessages` (the shared message formatter), standard codes, and declarative attributes (`[GenerateValidator]`, `[Required]`, `[Email]`, `[Range]`, etc.).

Use it when a project or domain layer needs to consume validation contracts without depending on the fluent DSL or ASP.NET Core — such as Application/Domain layers, messaging endpoints, workers, and custom validator implementations.

For the fluent DSL engine, use `eQuantic.Validation`; for ASP.NET Core Minimal APIs / MVC integration, use `eQuantic.Validation.AspNetCore`.
