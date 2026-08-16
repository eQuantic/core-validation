using System.Text;

namespace eQuantic.Validation.Testing;

/// <summary>
/// Thrown when a validation test assertion fails. Framework-agnostic: every test framework
/// (NUnit, xUnit, MSTest, TUnit) reports it as a failed test with the full message.
/// </summary>
public sealed class ValidationAssertionException : Exception
{
    /// <summary>Initializes the exception with a complete assertion message.</summary>
    public ValidationAssertionException(string message)
        : base(message)
    {
    }

    internal static ValidationAssertionException Create(
        string expectation,
        IReadOnlyCollection<ValidationFailure> relevantFailures)
    {
        var builder = new StringBuilder(expectation);
        if (relevantFailures.Count == 0)
        {
            builder.AppendLine().Append("The result contains no failures.");
        }
        else
        {
            builder.AppendLine().Append("Actual failures:").Append(Describe(relevantFailures));
        }

        return new ValidationAssertionException(builder.ToString());
    }

    internal static string Describe(IEnumerable<ValidationFailure> failures)
    {
        var builder = new StringBuilder();
        foreach (var failure in failures)
        {
            builder.AppendLine()
                .Append("  - ")
                .Append(failure.Path.Length == 0 ? "<model>" : failure.Path)
                .Append(" [").Append(failure.Code).Append(']')
                .Append(" (").Append(failure.Severity).Append("): ")
                .Append(failure.Message);

            if (failure.Arguments.Count > 0)
            {
                builder.Append("  {");
                var first = true;
                foreach (var argument in failure.Arguments)
                {
                    if (!first)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(argument.Key).Append(" = ").Append(argument.Value ?? "null");
                    first = false;
                }

                builder.Append('}');
            }
        }

        return builder.ToString();
    }
}
