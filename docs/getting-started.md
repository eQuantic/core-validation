# Getting started

Ten minutes from `dotnet add package` to a validated endpoint.

## Install

```bash
dotnet add package eQuantic.Validation
dotnet add package eQuantic.Validation.AspNetCore   # web apps
```

And the generator, in every project that declares models or validators:

```xml
<PackageReference Include="eQuantic.Validation.Generator"
                  PrivateAssets="all"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

## Your first validator, three ways

**1. Declarative — attributes + source generator.** Best for DTOs and commands; the validator is
generated at compile time, reflection-free:

```csharp
using eQuantic.Validation.Attributes;

[GenerateValidator]
public sealed record CreateCustomer(
    [Required, NotWhiteSpace] string Name,
    [Required, Email(Code = "customer.email.invalid")] string Email,
    [Range(18, 120)] int Age);
// the generator emits CreateCustomerGeneratedValidator for you
```

**2. Fluent — a class.** Best for domain rules, cross-field logic and injected dependencies:

```csharp
using eQuantic.Validation;

public sealed class CreateCustomerValidator : Validator<CreateCustomer>
{
    public CreateCustomerValidator(ICustomerDirectory customers)
    {
        RuleFor(x => x.Name).NotWhiteSpace().MinimumLength(2);
        RuleFor(x => x.Email)
            .NotWhiteSpace()
            .Email()
            .MustAsync(
                async (_, email, _, ct) => !await customers.EmailExistsAsync(email!, ct),
                code: "customer.email.taken");
    }
}
```

**3. Inline — no class at all.** Best for tests and quick composition:

```csharp
var validator = new InlineValidator<CreateCustomer>(v =>
{
    v.RuleFor(x => x.Email).NotWhiteSpace().Email();
});
```

## Validate

```csharp
var result = await validator.ValidateAsync(command);

if (!result.IsValid)
{
    foreach (var failure in result.Errors)
    {
        // failure.Path      => "Email"
        // failure.Code      => "customer.email.taken"   (stable, machine-readable)
        // failure.Message   => "Email is invalid."      (localizable)
        // failure.Severity  => Error | Warning | Information
        // failure.Arguments => { MinimumLength = 2, ... }
    }
}
```

Validators with asynchronous rules must be called through `ValidateAsync` — the synchronous
`Validate` throws `AsyncValidationRequiredException` instead of blocking, and the analyzer flags
the call site at build time (`VALGEN007`).

## Wire it into a web app

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGeneratedValidation();   // registers this assembly's validators (generated + fluent)
builder.Services.AddValidator<CreateOrder, CreateOrderValidator>();   // or explicitly, one by one
builder.Services.AddValidationLocalization(); // optional: built-in translated messages

var app = builder.Build();

app.MapPost("/customers", (CreateCustomer command) => Results.Created())
   .RequireValidation();          // 400 ProblemDetails on errors, warnings surface on 200

app.Run();
```

Invalid requests answer with RFC 7807 problem details, including structured `issues`:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Email": ["Email must be a valid email address."] },
  "issues": [
    { "path": "Email", "code": "customer.email.invalid", "message": "Email must be a valid email address.", "severity": "Error" }
  ]
}
```

## Where to go next

- Every rule, with examples: [Fluent rules reference](fluent-rules.md)
- What the generator does for you: [Source generator](source-generator.md)
- Warnings, deduplication and problem details: [ASP.NET Core](aspnetcore.md)
- Coming from FluentValidation? [The migration guide](migrating-from-fluentvalidation.md)
