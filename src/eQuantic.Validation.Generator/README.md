# eQuantic.Validation.Generator

Incremental Roslyn Source Generator producing zero-allocation validators and compile-time DI registration without assembly scanning. Fully compatible with Native AOT and trimmed applications.

```xml
<PackageReference Include="eQuantic.Validation.Generator"
                  Version="0.1.0"
                  PrivateAssets="all"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Install it in your application entrypoint project (e.g. your ASP.NET Core API) that references `eQuantic.Validation.AspNetCore` and the assemblies containing your models and validators:

```csharp
services.AddGeneratedValidation();
```

This extension method registers all generated and manual validators from the current assembly and referenced projects as `scoped` services, with zero runtime reflection.
