using eQuantic.Validation;
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Attributes;
using eQuantic.Validation.Generated;

var builder = WebApplication.CreateBuilder(args);

// OpenAPI and validation configuration
builder.Services.AddOpenApi(options =>
{
    options.AddValidationTransformer();
});

// Registers all generated and manual validators at compile-time with zero reflection:
builder.Services.AddGeneratedValidation();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var v1 = app.MapGroup("/api/v1");

// 1. Endpoint using compile-time Source Generated validation (Zero-Allocation)
v1.MapPost("/customers", (CreateCustomerRequest request) =>
{
    return Results.Created($"/api/v1/customers/{Guid.NewGuid()}", new
    {
        Message = "Customer registered successfully!",
        Customer = request
    });
}).RequireValidation("create");

// 2. Endpoint using fluent validator with cross-field Pattern Matching
v1.MapPost("/payments", (ProcessPaymentRequest request) =>
{
    return Results.Ok(new
    {
        Message = "Payment processed successfully!",
        Payment = request
    });
}).RequireValidation();

// 3. Endpoint using nested models and collection validation
v1.MapPost("/orders", (CreateOrderRequest request) =>
{
    return Results.Created($"/api/v1/orders/{Guid.NewGuid()}", new
    {
        Message = "Order created successfully!",
        Order = request
    });
}).RequireValidation();

app.Run();

// ==========================================
// Models and Validators
// ==========================================

// Model 1: Declarative Source Generated Model
[GenerateValidator]
public sealed record CreateCustomerRequest(
    [Required, NotWhiteSpace] string Name,
    [Required, Email(Code = "customer.email.invalid")] string Email,
    [eQuantic.Validation.Attributes.Range(18, 120)] int Age,
    [Pattern("^VIP-", Scenarios = ["vip"], Code = "customer.vip.prefix")] string? MembershipCode = null
);

// Model 2: Relational Pattern Matching Validator
public sealed record ProcessPaymentRequest(
    string Method,
    decimal Amount,
    string? CardNumber,
    string? PixKey
);

public sealed class ProcessPaymentValidator : Validator<ProcessPaymentRequest>
{
    public ProcessPaymentValidator()
    {
        RuleFor(x => x.Method)
            .NotWhiteSpace()
            .Must(m => m is "PIX" or "CREDIT", code: "payment.method.unsupported")
            .WithMessage("Only PIX and CREDIT methods are supported.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithCode("payment.amount.invalid");

        // Modern relational pattern matching:
        RuleForModel()
            .Match(
                static p => p is { Method: "PIX", CardNumber: not null },
                targetPropertyPath: nameof(ProcessPaymentRequest.CardNumber),
                code: "payment.pix.no_card",
                messageTemplate: "Pix payment must not contain a card number.")
            .Match(
                static p => p is { Method: "PIX", PixKey: null },
                targetPropertyPath: nameof(ProcessPaymentRequest.PixKey),
                code: "payment.pix.key_required",
                messageTemplate: "Pix key is required for Pix payments.")
            .Match(
                static p => p is { Method: "CREDIT", CardNumber: null },
                targetPropertyPath: nameof(ProcessPaymentRequest.CardNumber),
                code: "payment.credit.card_required",
                messageTemplate: "Card number is required for Credit payments.");
    }
}

// Model 3: Nested models and collections with Source Generator
[GenerateValidator]
public sealed record OrderItemDto(
    [Required, NotWhiteSpace] string ProductSku,
    [eQuantic.Validation.Attributes.Range(1, 100)] int Quantity,
    [GreaterThan(0)] decimal UnitPrice
);

[GenerateValidator]
public sealed record CreateOrderRequest(
    [Required, NotWhiteSpace] string CustomerId,
    [ValidateEach] IReadOnlyList<OrderItemDto> Items
);
