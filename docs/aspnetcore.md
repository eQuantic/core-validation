# ASP.NET Core

`eQuantic.Validation.AspNetCore` integrates validators with Minimal APIs, MVC, problem details,
OpenAPI and localization. Everything is opt-in per endpoint or per pipeline — no global magic.

## Minimal APIs

```csharp
app.MapPost("/customers", (CreateCustomer command) => Results.Created())
   .RequireValidation();                 // validate every argument with a registered validator

app.MapPost("/customers", handler)
   .RequireValidation("create");         // additionally activate the "create" scenario

var v1 = app.MapGroup("/api/v1").RequireValidation();   // or for a whole group
```

## MVC controllers

```csharp
builder.Services.AddControllers().AddValidationFilter();
```

## The 400 response shape

Blocking errors return RFC 7807/9457 problem details. `errors` keeps compatibility with standard
ASP.NET Core clients; `issues` carries the structured failures:

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

`issues` is emitted as JSON nodes, so source-generated serialization and Native AOT publish work.

## Warnings on successful responses

`Severity.Warning` rules don't block — and they don't die on the server. Valid requests carrying
warnings answer with a compact header, and handlers can read the full failures:

```http
HTTP/1.1 200 OK
Validation-Warnings: Nickname:profile.nickname.legacy
```

```csharp
app.MapPut("/profile", (UpdateProfile request, HttpContext http) =>
{
    var warnings = ValidationWarnings.GetWarnings(http);   // full path/code/message/arguments
    return Results.Ok(new { saved = true, warnings });
}).RequireValidation();
```

No warnings → no header, no items; existing responses are untouched.

## Execution mode: sequential by default

Rules run sequentially. Parallel execution is opt-in per endpoint and requires every async rule
dependency to be safe for concurrent use — a scoped EF Core `DbContext` is **not**:

```csharp
app.MapPost("/quotes", handler).RequireValidation(ValidationExecutionMode.Parallel);
builder.Services.AddControllers().AddValidationFilter(ValidationExecutionMode.Parallel);
```

## Async deduplication, automatic

The filters and the dispatcher create one validation context per request with async-rule
deduplication enabled: the same `MustAsync` for the same instance and value (composed validators,
duplicated collection elements, revalidation) performs its I/O once per request. Standalone code
opts in explicitly:

```csharp
var context = new ValidationContext(deduplicateAsyncRules: true);
await validator.ValidateAsync(model, context);
```

## OpenAPI (.NET 10)

```csharp
builder.Services.AddOpenApi(options => options.AddValidationTransformer());
```

Schemas are enriched from declarative attributes **and** from every registered validator that
implements `IDescribableValidator` — fluent `MinimumLength`, `Matches`, `GreaterThan` land in the
contract as `minLength`, `pattern`, `exclusiveMinimum`. See [Rule manifest & export](manifest-and-export.md).

## Beyond HTTP

`IValidationDispatcher` lives in the core package: workers, gRPC services and message consumers
validate with the same registered validators, without referencing ASP.NET Core:

```csharp
public sealed class CreateCustomerConsumer(IValidationDispatcher dispatcher, IServiceProvider services)
{
    public async Task Handle(CreateCustomer message, CancellationToken ct)
    {
        var result = await dispatcher.ValidateAsync(message, services, cancellationToken: ct);
        if (!result.IsValid) { /* nack / dead-letter with result.Errors */ }
    }
}
```
