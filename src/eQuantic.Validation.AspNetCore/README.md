# eQuantic.Validation.AspNetCore

Integra o contrato `eQuantic.Validation.Abstractions` a Minimal APIs e MVC em .NET 8 e .NET 10.

```bash
dotnet add package eQuantic.Validation.AspNetCore
```

Registre cada implementação de `IValidator<T>` explicitamente:

```csharp
builder.Services
    .AddValidation()
    .AddValidator<CreateOrder, CreateOrderValidator>();
```

Em Minimal APIs, adicione o filtro aos endpoints que devem validar:

```csharp
app.MapPost("/orders", (CreateOrder command) => Results.Ok())
   .RequireValidation("create");
```

Em controllers, habilite o filtro global:

```csharp
builder.Services.AddControllers().AddValidation();
```

As falhas bloqueantes retornam HTTP 400 como `ValidationProblemDetails`; o campo `errors` mantém compatibilidade com clientes ASP.NET Core e `extensions.issues` preserva `code`, `path` e `severity`.
