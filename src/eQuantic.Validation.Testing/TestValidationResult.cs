using System.Linq.Expressions;
using eQuantic.Validation.Internal;

namespace eQuantic.Validation.Testing;

/// <summary>
/// A validation outcome wrapped with test assertions. Paths can be addressed as strings
/// (<c>"Address.PostalCode"</c>, <c>"Contacts[0].Email"</c>) or as typed expressions
/// (<c>x =&gt; x.Address.PostalCode</c>).
/// </summary>
/// <typeparam name="T">The validated model type.</typeparam>
public sealed class TestValidationResult<T>
{
    /// <summary>Initializes the wrapper over a raw validation result.</summary>
    public TestValidationResult(ValidationResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>Gets the underlying validation result for manual inspection.</summary>
    public ValidationResult Result { get; }

    /// <summary>Gets every failure, including warnings and informational issues.</summary>
    public IReadOnlyList<ValidationFailure> Failures => Result.Failures;

    /// <summary>Asserts that the result has no blocking errors (warnings are allowed).</summary>
    public TestValidationResult<T> ShouldBeValid()
    {
        if (!Result.IsValid)
        {
            throw ValidationAssertionException.Create(
                "Expected the result to be valid, but it has blocking errors.",
                Result.Errors);
        }

        return this;
    }

    /// <summary>Asserts that the result has at least one blocking error.</summary>
    public TestValidationResult<T> ShouldBeInvalid()
    {
        if (Result.IsValid)
        {
            throw ValidationAssertionException.Create(
                "Expected the result to be invalid, but it has no blocking errors.",
                Result.Failures);
        }

        return this;
    }

    /// <summary>Asserts that at least one failure targets <paramref name="path"/> and returns a chain to refine it.</summary>
    public TestFailureAssertions ShouldHaveFailureFor(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A property path is required.", nameof(path));
        }

        var matched = FailuresFor(path);
        if (matched.Length == 0)
        {
            throw ValidationAssertionException.Create(
                $"Expected a failure for path '{path}', but none targets it.",
                Result.Failures);
        }

        return new TestFailureAssertions($"for path '{path}'", matched);
    }

    /// <summary>Asserts that at least one failure targets the member selected by <paramref name="expression"/>.</summary>
    public TestFailureAssertions ShouldHaveFailureFor(Expression<Func<T, object?>> expression)
        => ShouldHaveFailureFor(PathOf(expression));

    /// <summary>Asserts that no failure of any severity targets <paramref name="path"/>.</summary>
    public TestValidationResult<T> ShouldNotHaveFailureFor(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A property path is required.", nameof(path));
        }

        var matched = FailuresFor(path);
        if (matched.Length > 0)
        {
            throw ValidationAssertionException.Create(
                $"Expected no failures for path '{path}', but found {matched.Length}.",
                matched);
        }

        return this;
    }

    /// <summary>Asserts that no failure of any severity targets the member selected by <paramref name="expression"/>.</summary>
    public TestValidationResult<T> ShouldNotHaveFailureFor(Expression<Func<T, object?>> expression)
        => ShouldNotHaveFailureFor(PathOf(expression));

    /// <summary>Asserts that at least one failure carries <paramref name="code"/>, anywhere in the result.</summary>
    public TestFailureAssertions ShouldHaveFailureWithCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A validation code is required.", nameof(code));
        }

        var matched = Result.Failures
            .Where(failure => string.Equals(failure.Code, code, StringComparison.Ordinal))
            .ToArray();
        if (matched.Length == 0)
        {
            throw ValidationAssertionException.Create(
                $"Expected a failure with code '{code}', but none carries it.",
                Result.Failures);
        }

        return new TestFailureAssertions($"with code '{code}'", matched);
    }

    private ValidationFailure[] FailuresFor(string path) =>
        Result.Failures
            .Where(failure => string.Equals(failure.Path, path, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static string PathOf(Expression<Func<T, object?>> expression)
    {
        if (expression is null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        var path = PropertyPath.FromExpression(expression);
        if (path.Length == 0)
        {
            throw new ArgumentException(
                "The expression must select a member, e.g. 'model => model.Email'.",
                nameof(expression));
        }

        return path;
    }
}
