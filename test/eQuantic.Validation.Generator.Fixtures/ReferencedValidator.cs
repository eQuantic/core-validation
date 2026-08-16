namespace eQuantic.Validation.GeneratorFixtures;

public sealed record ReferencedGeneratorRequest(string? Email);

public sealed class ReferencedGeneratorValidator : Validator<ReferencedGeneratorRequest>
{
    public ReferencedGeneratorValidator()
    {
        RuleFor(request => request.Email)
            .Email()
            .WithCode("referenced.email.invalid");
    }
}
