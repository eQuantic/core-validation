namespace eQuantic.Validation.Testing;

/// <summary>
/// Chainable assertions over a set of matched failures. Each <c>With*</c> call narrows the set
/// and throws <see cref="ValidationAssertionException"/> when nothing remains, printing the
/// failures that were actually present.
/// </summary>
public sealed class TestFailureAssertions
{
    private readonly string _subject;
    private readonly IReadOnlyList<ValidationFailure> _current;

    internal TestFailureAssertions(string subject, IReadOnlyList<ValidationFailure> current)
    {
        _subject = subject;
        _current = current;
    }

    /// <summary>Gets the failures currently matched by this assertion chain.</summary>
    public IReadOnlyList<ValidationFailure> Failures => _current;

    /// <summary>Requires at least one matched failure to carry the given stable code.</summary>
    public TestFailureAssertions WithCode(string code) =>
        Narrow(
            failure => string.Equals(failure.Code, code, StringComparison.Ordinal),
            $"with code '{code}'");

    /// <summary>Requires at least one matched failure to carry the exact user-facing message.</summary>
    public TestFailureAssertions WithMessage(string message) =>
        Narrow(
            failure => string.Equals(failure.Message, message, StringComparison.Ordinal),
            $"with message '{message}'");

    /// <summary>Requires at least one matched failure to carry the given severity.</summary>
    public TestFailureAssertions WithSeverity(ValidationSeverity severity) =>
        Narrow(
            failure => failure.Severity == severity,
            $"with severity '{severity}'");

    /// <summary>Requires at least one matched failure to expose the given rule argument.</summary>
    public TestFailureAssertions WithArgument(string name, object? value) =>
        Narrow(
            failure => failure.Arguments.TryGetValue(name, out var actual) && Equals(actual, value),
            $"with argument {name} = {value ?? "null"}");

    /// <summary>Requires the current chain to have matched exactly <paramref name="count"/> failures.</summary>
    public TestFailureAssertions Exactly(int count)
    {
        if (_current.Count != count)
        {
            throw ValidationAssertionException.Create(
                $"Expected exactly {count} failure(s) {_subject}, but found {_current.Count}.",
                _current);
        }

        return this;
    }

    private TestFailureAssertions Narrow(Func<ValidationFailure, bool> predicate, string description)
    {
        var narrowed = _current.Where(predicate).ToArray();
        if (narrowed.Length == 0)
        {
            throw ValidationAssertionException.Create(
                $"Expected a failure {_subject} {description}, but none matched.",
                _current);
        }

        return new TestFailureAssertions(_subject + " " + description, narrowed);
    }
}
