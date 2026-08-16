# eQuantic.Validation.Testing

Test helpers for eQuantic.Validation: `TestValidate` with chainable, framework-agnostic assertions over structured failures. Works with NUnit, xUnit, MSTest and TUnit — failed assertions throw `ValidationAssertionException` with the full list of actual failures.

```bash
dotnet add package eQuantic.Validation.Testing
```

```csharp
using eQuantic.Validation.Testing;

var validator = new CreateCustomerValidator();

// Paths as typed expressions or strings, refined by code, severity, message or arguments:
validator.TestValidate(new CreateCustomer(Name: "", Email: "nope", Age: 15))
    .ShouldBeInvalid()
    .ShouldHaveFailureFor(x => x.Email).WithCode("customer.email.invalid");

validator.TestValidate(valid)
    .ShouldBeValid()
    .ShouldNotHaveFailureFor(x => x.Email);

// Scenarios and async rules:
var result = await validator.TestValidateAsync(customer, "create");
result.ShouldHaveFailureWithCode("customer.email.taken").WithSeverity(ValidationSeverity.Error);

// Rule arguments are part of the failure model, so tests never match on message strings:
validator.TestValidate(new CreateCustomer(Name: "A", ...))
    .ShouldHaveFailureFor("Name").WithArgument("MinimumLength", 2).Exactly(1);
```

Assertions match failures of any severity; `ShouldBeValid` follows the engine semantics (warnings do not block). Nested and collection paths use the canonical form: `"Address.PostalCode"`, `"Contacts[0].Email"`.
