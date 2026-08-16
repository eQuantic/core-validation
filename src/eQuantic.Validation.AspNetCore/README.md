# eQuantic.Validation.AspNetCore

ASP.NET Core integration for `eQuantic.Validation.Abstractions`, supporting Minimal APIs and MVC in .NET 8 and .NET 10.

```bash
dotnet add package eQuantic.Validation.AspNetCore
```

### Quick Start

1. Register validation services in your `Program.cs`:

```csharp
builder.Services
    .AddValidation()
    .AddValidator<CreateOrder, CreateOrderValidator>();
```

2. For Minimal APIs, apply the endpoint filter:

```csharp
app.MapPost("/orders", (CreateOrder command) => Results.Ok())
   .RequireValidation("create");
```

3. For MVC Controllers, enable the global validation action filter:

```csharp
builder.Services.AddControllers().AddValidation();
```

4. Enable OpenAPI 3.1 schema transformations and per-request localization:

```csharp
builder.Services.AddValidationLocalization<ValidationResources>();
builder.Services.AddOpenApi(options =>
{
    options.AddValidationTransformer();
});
```

Failed validation requests automatically return HTTP 400 with RFC 7807 / RFC 9457 `ValidationProblemDetails`. The `errors` dictionary maintains ASP.NET Core client compatibility, while `extensions.issues` provides structured `code`, `path`, and `severity`.
