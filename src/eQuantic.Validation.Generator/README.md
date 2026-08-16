# eQuantic.Validation.Generator

Incremental Roslyn source generator producing reflection-free, low-allocation validators and compile-time DI registration without assembly scanning. Trimming- and Native AOT-friendly: nested and collection validators resolve through typed `IValidator<T>` services, never through `MakeGenericType`.

```xml
<PackageReference Include="eQuantic.Validation.Generator"
                  PrivateAssets="all"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Install it in every project that declares models with `[GenerateValidator]` or manual validators, then register that assembly's validators:

```csharp
services.AddGeneratedValidation();
```

`AddGeneratedValidation` is generated as `internal` to the compiling assembly and registers only the validators declared or generated **in that assembly** as `scoped` services. Referenced assemblies are never scanned — each library opts in by exposing its own registration (or the host composes explicitly with `AddValidator<TModel, TValidator>`). This keeps registration deterministic, incremental-build-friendly and free of surprise registrations.

The generator reports compile-time diagnostics for misuse: `VALGEN002` (missing `[CustomRule]` method), `VALGEN003` (unresolvable `[ValidateEach]` element type) and `VALGEN004` (rule applied to an incompatible property type).
