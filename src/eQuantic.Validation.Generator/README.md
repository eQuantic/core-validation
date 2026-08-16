# eQuantic.Validation.Generator

Gerador incremental que produz validadores e cria o registro DI em tempo de compilação sem assembly scanning. Ideal para aplicações trimadas ou Native AOT.

```xml
<PackageReference Include="eQuantic.Validation.Generator"
                  Version="0.1.0"
                  PrivateAssets="all"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

Instale-o no projeto de composição (por exemplo, a API) que referencia `eQuantic.Validation.AspNetCore` e os assemblies que contêm seus modelos e validadores:

```csharp
services.AddGeneratedValidation();
```

Esse método registra todos os validadores gerados e manuais da aplicação e de assemblies referenciados como `scoped`, sem reflexão em runtime.
