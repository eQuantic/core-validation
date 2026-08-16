# Localization

Messages are resolved in two steps: an optional `IValidationMessageProvider` picks the template,
then `{Property}` and rule arguments (`{MinimumLength}`, `{Minimum}`, …) are substituted. This
works identically for fluent and source-generated validators.

## Built-in translations (zero setup)

The library ships translations for its default messages in **en, pt, es, fr, de, it**:

```csharp
builder.Services.AddValidationLocalization();   // registers BuiltInValidationMessages
app.UseRequestLocalization();                   // Accept-Language drives CurrentUICulture
```

```http
POST /customers
Accept-Language: pt-BR

HTTP/1.1 400 Bad Request
{ "errors": { "Name": ["Name deve conter pelo menos 2 caracteres."] } }
```

Two guarantees:

- **Custom messages are never touched.** `WithMessage("...")` (or `Message = "..."` on
  attributes) always wins — only the stock English templates are translated.
- **Unsupported cultures fall back to English.**

Outside ASP.NET Core, pass the provider explicitly:

```csharp
var context = new ValidationContext(messageProvider: BuiltInValidationMessages.Instance);
```

## Your own resources via IStringLocalizer

For your own languages or message overrides, register a resource-based provider:

```csharp
builder.Services.AddLocalization();
builder.Services.AddValidationLocalization<ValidationResources>();
```

The provider looks messages up in two passes:

1. **By stable code** — a resx entry named `customer.email.taken` wins for that rule wherever it
   fires. This is the recommended key: codes survive message rewording.
2. **By template** — an entry named `{Property} is required.` translates every rule that still
   uses that default template.

```xml
<!-- ValidationResources.pt.resx -->
<data name="customer.email.taken"><value>Este e-mail já está em uso.</value></data>
<data name="{Property} is required."><value>{Property} é obrigatório.</value></data>
```

Templates keep their placeholders; substitution happens after lookup, so `{MinimumLength}` works
in any language.

## Writing your own provider

Implement one interface — the descriptor gives you the path, display name, stable code, kind,
default template and arguments:

```csharp
public sealed class MyMessages : IValidationMessageProvider
{
    public string Resolve(ValidationMessageDescriptor descriptor) =>
        descriptor.Code switch
        {
            "customer.email.taken" => "This email is taken.",
            _ => descriptor.Template,
        };
}

builder.Services.AddSingleton<IValidationMessageProvider>(new MyMessages());
```
