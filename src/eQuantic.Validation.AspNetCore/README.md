# eQuantic.Validation.AspNetCore

ASP.NET Core integration for `eQuantic.Validation`, supporting Minimal APIs and MVC in .NET 8 and .NET 10.

```bash
dotnet add package eQuantic.Validation.AspNetCore
```

### Quick Start

1. Register validators in your `Program.cs` (`AddValidator` registers the validation dispatcher
   automatically; the names avoid colliding with .NET 10's built-in `AddValidation`):

```csharp
builder.Services.AddValidator<CreateOrder, CreateOrderValidator>();
```

2. For Minimal APIs, apply the endpoint filter. Rules run sequentially by default; parallel
   execution is opt-in and requires concurrency-safe rule dependencies:

```csharp
app.MapPost("/orders", (CreateOrder command) => Results.Ok())
   .RequireValidation("create");

app.MapPost("/quotes", handler)
   .RequireValidation(ValidationExecutionMode.Parallel);
```

3. For MVC Controllers, enable the global validation action filter:

```csharp
builder.Services.AddControllers().AddValidationFilter();
```

4. Enable OpenAPI 3.1 schema transformations and per-request localization (combine with
   `UseRequestLocalization` so `Accept-Language` drives the culture):

```csharp
builder.Services.AddValidationLocalization<ValidationResources>();
builder.Services.AddOpenApi(options =>
{
    options.AddValidationTransformer();
});
```

Failed validation requests automatically return HTTP 400 with RFC 7807 / RFC 9457 `ValidationProblemDetails`. The `errors` dictionary maintains ASP.NET Core client compatibility, while `extensions.issues` provides structured `code`, `path`, and `severity` (emitted as JSON nodes, safe for source-generated serialization and Native AOT):

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

### Warnings on successful responses

Valid requests with `Severity.Warning` failures respond `200` with a `Validation-Warnings`
header (`path:code` pairs). The full failures are available to the handler:

```csharp
app.MapPut("/profile", (UpdateProfile request, HttpContext http) =>
{
    var warnings = ValidationWarnings.GetWarnings(http);
    return Results.Ok(new { saved = true, warnings });
}).RequireValidation();
```

### Async rule deduplication

The filters create one validation context per request with async-rule deduplication enabled:
the same `MustAsync` for the same instance and value (composed validators, duplicated collection
elements, revalidation) performs its I/O once per request. No configuration needed.
