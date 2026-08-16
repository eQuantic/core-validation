# Testing validators

`eQuantic.Validation.Testing` works with any test framework — failed assertions throw
`ValidationAssertionException` with the full list of actual failures, which NUnit, xUnit, MSTest
and TUnit all report as a failed test with a readable message.

```bash
dotnet add package eQuantic.Validation.Testing
```

## TestValidate

```csharp
using eQuantic.Validation.Testing;

var validator = new CreateCustomerValidator();

validator.TestValidate(new CreateCustomer(Name: "", Email: "nope", Age: 15))
    .ShouldBeInvalid()
    .ShouldHaveFailureFor(x => x.Email).WithCode("customer.email.invalid");

validator.TestValidate(valid)
    .ShouldBeValid()                       // warnings allowed — matches engine semantics
    .ShouldNotHaveFailureFor(x => x.Email);
```

Paths are typed expressions or strings — nested and collection paths use the canonical form:

```csharp
result.ShouldHaveFailureFor(x => x.Address.Street);
result.ShouldHaveFailureFor("Contacts[1].Email");
```

## Assert on codes and arguments, not message strings

Because failures are structured, tests never match on message text — they survive rewording and
localization:

```csharp
validator.TestValidate(new CreateCustomer(Name: "A", ...))
    .ShouldHaveFailureFor("Name")
        .WithCode(ValidationCodes.MinimumLength)
        .WithSeverity(ValidationSeverity.Error)
        .WithArgument("MinimumLength", 2)
        .Exactly(1);
```

Each `With*` narrows the matched set; when nothing remains, the exception prints what actually
happened:

```
Expected a failure for path 'Email' with code 'wrong.code', but none matched.
Actual failures:
  - Email [email] (Error): Email must be a valid email address.
```

## Scenarios and async rules

```csharp
var result = await validator.TestValidateAsync(customer, "create");   // scenario overloads
result.ShouldHaveFailureWithCode("customer.email.taken");

var updateResult = await validator.TestValidateAsync(customer, "update");
updateResult.ShouldBeValid();
```

## Warnings

```csharp
validator.TestValidate(legacyProfile)
    .ShouldBeValid()   // warnings do not block
    .ShouldHaveFailureFor(x => x.Nickname).WithSeverity(ValidationSeverity.Warning);
```

## Escaping the helpers

`TestValidationResult<T>` exposes the raw result when you need custom assertions:

```csharp
var result = validator.TestValidate(model);
IReadOnlyList<ValidationFailure> failures = result.Failures;
ValidationResult raw = result.Result;
```
