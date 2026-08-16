using System.Text.RegularExpressions;

namespace eQuantic.Validation;

/// <summary>
/// String-only rules, exposed as extensions over <see cref="IRuleBuilder{TModel, TProperty}"/> so
/// they can only be chained on <c>string</c> properties. All rules are null-permissive: a
/// <see langword="null"/> value passes, combine with <c>NotNull()</c> or <c>NotEmpty()</c> to
/// require a value.
/// </summary>
public static class StringRuleExtensions
{
    private static readonly Regex EmailPattern = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.CultureInvariant);

    /// <summary>Requires a non-empty, non-whitespace string.</summary>
    public static RuleBuilder<TModel, string?> NotWhiteSpace<TModel>(this IRuleBuilder<TModel, string?> builder)
    {
        return Builder(builder).Add(
            static (_, value) => value is not null && !string.IsNullOrWhiteSpace(value),
            ValidationCodes.NotWhiteSpace,
            "{Property} must not be blank.",
            kind: ValidationRuleKinds.NotWhiteSpace);
    }

    /// <summary>Requires a value with a conventional email address shape.</summary>
    public static RuleBuilder<TModel, string?> Email<TModel>(this IRuleBuilder<TModel, string?> builder)
    {
        return Builder(builder).Add(
            static (_, value) => value is null || EmailPattern.IsMatch(value),
            ValidationCodes.Email,
            "{Property} must be a valid email address.",
            kind: ValidationRuleKinds.Email);
    }

    /// <summary>Requires the string to contain at least <paramref name="minimumLength"/> characters.</summary>
    public static RuleBuilder<TModel, string?> MinimumLength<TModel>(
        this IRuleBuilder<TModel, string?> builder,
        int minimumLength)
    {
        if (minimumLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        }

        return Builder(builder).Add(
            (_, value) => value is null || value.Length >= minimumLength,
            ValidationCodes.MinimumLength,
            "{Property} must contain at least {MinimumLength} characters.",
            new Dictionary<string, object?> { ["MinimumLength"] = minimumLength },
            kind: ValidationRuleKinds.MinimumLength);
    }

    /// <summary>Requires the string to contain no more than <paramref name="maximumLength"/> characters.</summary>
    public static RuleBuilder<TModel, string?> MaximumLength<TModel>(
        this IRuleBuilder<TModel, string?> builder,
        int maximumLength)
    {
        if (maximumLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        return Builder(builder).Add(
            (_, value) => value is null || value.Length <= maximumLength,
            ValidationCodes.MaximumLength,
            "{Property} must contain no more than {MaximumLength} characters.",
            new Dictionary<string, object?> { ["MaximumLength"] = maximumLength },
            kind: ValidationRuleKinds.MaximumLength);
    }

    /// <summary>Requires the string length to be inside an inclusive range.</summary>
    public static RuleBuilder<TModel, string?> Length<TModel>(
        this IRuleBuilder<TModel, string?> builder,
        int minimumLength,
        int maximumLength)
    {
        if (minimumLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumLength));
        }

        if (maximumLength < minimumLength)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLength));
        }

        return Builder(builder).Add(
            (_, value) => value is null || (value.Length >= minimumLength && value.Length <= maximumLength),
            ValidationCodes.Range,
            "{Property} must contain between {MinimumLength} and {MaximumLength} characters.",
            new Dictionary<string, object?>
            {
                ["MinimumLength"] = minimumLength,
                ["MaximumLength"] = maximumLength,
            },
            kind: ValidationRuleKinds.Length);
    }

    /// <summary>Requires the string to match <paramref name="pattern"/>.</summary>
    public static RuleBuilder<TModel, string?> Matches<TModel>(
        this IRuleBuilder<TModel, string?> builder,
        string pattern,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            throw new ArgumentException("A regular expression pattern is required.", nameof(pattern));
        }

        var regex = new Regex(pattern, options);
        return Builder(builder).Add(
            (_, value) => value is null || regex.IsMatch(value),
            ValidationCodes.Pattern,
            "{Property} has an invalid format.",
            new Dictionary<string, object?> { ["Pattern"] = pattern },
            kind: ValidationRuleKinds.Pattern);
    }

    private static RuleBuilder<TModel, string?> Builder<TModel>(IRuleBuilder<TModel, string?> builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        // RuleBuilder<TModel, string> and RuleBuilder<TModel, string?> are the same runtime type;
        // the cast always succeeds for builders produced by RuleFor.
        return (RuleBuilder<TModel, string?>)builder;
    }
}
