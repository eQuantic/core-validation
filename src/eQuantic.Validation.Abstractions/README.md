# eQuantic.Validation.Abstractions

Contratos estáveis e sem dependência de framework para o ecossistema eQuantic.Validation.

```bash
dotnet add package eQuantic.Validation.Abstractions
```

O pacote contém `IValidator<T>`, `ValidationContext`, `ValidationResult`, `ValidationFailure`,
códigos e severidades. Use-o quando uma camada precisa **consumir** validação sem depender da DSL
fluent ou de ASP.NET Core — por exemplo, contratos de Application/Domain, endpoints, workers e
implementações próprias de validadores.

Para a implementação fluent use `eQuantic.Validation`; para a integração web use
`eQuantic.Validation.AspNetCore`.
