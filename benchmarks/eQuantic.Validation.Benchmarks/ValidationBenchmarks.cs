using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using eQuantic.Validation.Attributes;
using FluentValidation;

namespace eQuantic.Validation.Benchmarks;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ValidationBenchmarks
{
    private BenchmarkCustomer _validCustomer = null!;
    private BenchmarkCustomer _invalidCustomer = null!;

    private BenchmarkCustomerGeneratedValidator _sourceGenValidator = null!;
    private EquanticCustomerFluentValidator _equanticFluentValidator = null!;
    private FluentValidationCustomerValidator _fvValidator = null!;

    [GlobalSetup]
    public void Setup()
    {
        _validCustomer = new BenchmarkCustomer(
            Name: "John Doe",
            Email: "john.doe@example.com",
            Age: 32,
            Document: "12345678901");

        _invalidCustomer = new BenchmarkCustomer(
            Name: "",
            Email: "invalid-email-address",
            Age: 15,
            Document: "123");

        _sourceGenValidator = new BenchmarkCustomerGeneratedValidator();
        _equanticFluentValidator = new EquanticCustomerFluentValidator();
        _fvValidator = new FluentValidationCustomerValidator();
    }

    [Benchmark(Baseline = true, Description = "eQuantic (Source Generated) - Valid")]
    public ValidationResult SourceGen_Valid()
    {
        return _sourceGenValidator.Validate(_validCustomer);
    }

    [Benchmark(Description = "eQuantic (Fluent DSL) - Valid")]
    public ValidationResult EquanticFluent_Valid()
    {
        return _equanticFluentValidator.Validate(_validCustomer);
    }

    [Benchmark(Description = "FluentValidation - Valid")]
    public global::FluentValidation.Results.ValidationResult FluentValidation_Valid()
    {
        return _fvValidator.Validate(_validCustomer);
    }

    [Benchmark(Description = "eQuantic (Source Generated) - Invalid")]
    public ValidationResult SourceGen_Invalid()
    {
        return _sourceGenValidator.Validate(_invalidCustomer);
    }

    [Benchmark(Description = "eQuantic (Fluent DSL) - Invalid")]
    public ValidationResult EquanticFluent_Invalid()
    {
        return _equanticFluentValidator.Validate(_invalidCustomer);
    }

    [Benchmark(Description = "FluentValidation - Invalid")]
    public global::FluentValidation.Results.ValidationResult FluentValidation_Invalid()
    {
        return _fvValidator.Validate(_invalidCustomer);
    }
}

[GenerateValidator]
public sealed record BenchmarkCustomer(
    [property: Required, MinLength(2), MaxLength(100)] string Name,
    [property: Required, Email] string Email,
    [property: Range(18, 120)] int Age,
    [property: Required, Length(11, 14)] string Document);

public sealed class EquanticCustomerFluentValidator : Validator<BenchmarkCustomer>
{
    public EquanticCustomerFluentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Email).NotEmpty().Email();
        RuleFor(x => x.Age).InclusiveBetween(18, 120);
        RuleFor(x => x.Document).NotEmpty().Length(11, 14);
    }
}

public sealed class FluentValidationCustomerValidator : AbstractValidator<BenchmarkCustomer>
{
    public FluentValidationCustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Age).InclusiveBetween(18, 120);
        RuleFor(x => x.Document).NotEmpty().Length(11, 14);
    }
}
