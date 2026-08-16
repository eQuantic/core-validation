using System.Collections;
using eQuantic.Validation.Internal;

namespace eQuantic.Validation;

/// <summary>
/// Fluent builder for a property validation rule. Comparison and shape rules are null-permissive:
/// a <see langword="null"/> value passes, combine with <see cref="NotNull"/> or <see cref="NotEmpty"/>
/// to require a value. String-specific rules live in <see cref="StringRuleExtensions"/> and only
/// bind to <c>string</c> properties.
/// </summary>
/// <typeparam name="T">The model being validated.</typeparam>
/// <typeparam name="TProperty">The property type.</typeparam>
public sealed class RuleBuilder<T, TProperty> : IRuleBuilder<T, TProperty>
{
    private readonly PropertyRule<T, TProperty> _rule;

    internal RuleBuilder(PropertyRule<T, TProperty> rule) => _rule = rule;

    /// <summary>Requires a non-null property value.</summary>
    public RuleBuilder<T, TProperty> NotNull()
    {
        return Add(
            static (_, value) => value is not null,
            ValidationCodes.Required,
            "{Property} is required.");
    }

    /// <summary>Requires a non-empty value. Strings may still contain whitespace.</summary>
    public RuleBuilder<T, TProperty> NotEmpty()
    {
        return Add(
            static (_, value) => !IsEmpty(value),
            ValidationCodes.NotEmpty,
            "{Property} must not be empty.");
    }

    /// <summary>Requires equality with <paramref name="expected"/>.</summary>
    public RuleBuilder<T, TProperty> EqualTo(TProperty expected)
    {
        return Add(
            (_, value) => EqualityComparer<TProperty>.Default.Equals(value, expected),
            ValidationCodes.Predicate,
            "{Property} has an unexpected value.");
    }

    /// <summary>Requires the value to be greater than <paramref name="minimum"/>.</summary>
    public RuleBuilder<T, TProperty> GreaterThan(TProperty minimum)
    {
        return Add(
            (_, value) => value is null || Comparer<TProperty>.Default.Compare(value, minimum) > 0,
            ValidationCodes.Range,
            "{Property} must be greater than {Minimum}.",
            new Dictionary<string, object?> { ["Minimum"] = minimum });
    }

    /// <summary>Requires the value to be less than <paramref name="maximum"/>.</summary>
    public RuleBuilder<T, TProperty> LessThan(TProperty maximum)
    {
        return Add(
            (_, value) => value is null || Comparer<TProperty>.Default.Compare(value, maximum) < 0,
            ValidationCodes.Range,
            "{Property} must be less than {Maximum}.",
            new Dictionary<string, object?> { ["Maximum"] = maximum });
    }

    /// <summary>Requires the value to be inside an inclusive range.</summary>
    public RuleBuilder<T, TProperty> InclusiveBetween(TProperty minimum, TProperty maximum)
    {
        if (Comparer<TProperty>.Default.Compare(minimum, maximum) > 0)
        {
            throw new ArgumentException("The minimum cannot be greater than the maximum.", nameof(minimum));
        }

        return Add(
            (_, value) => value is null ||
                          Comparer<TProperty>.Default.Compare(value, minimum) >= 0 &&
                          Comparer<TProperty>.Default.Compare(value, maximum) <= 0,
            ValidationCodes.Range,
            "{Property} must be between {Minimum} and {Maximum}.",
            new Dictionary<string, object?>
            {
                ["Minimum"] = minimum,
                ["Maximum"] = maximum,
            });
    }

    /// <summary>Adds a synchronous custom predicate for this property.</summary>
    public RuleBuilder<T, TProperty> Must(Func<TProperty, bool> predicate, string code = ValidationCodes.Predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Add((_, value) => predicate(value), code, "{Property} is invalid.");
    }

    /// <summary>Adds a synchronous custom predicate with access to the complete model.</summary>
    public RuleBuilder<T, TProperty> Must(Func<T, TProperty, bool> predicate, string code = ValidationCodes.Predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return Add(predicate, code, "{Property} is invalid.");
    }

    /// <summary>Adds an asynchronous, cancellation-aware custom predicate.</summary>
    public RuleBuilder<T, TProperty> MustAsync(
        Func<T, TProperty, ValidationContext, CancellationToken, Task<bool>> predicate,
        string code = ValidationCodes.Predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        _rule.Add(new ValidationCheck<T, TProperty>(null, predicate, code, "{Property} is invalid."));
        return this;
    }

    /// <summary>
    /// Validates that the property value can successfully instantiate a domain Value Object or type via a factory method.
    /// The rule FAILS if the factory returns <see langword="null"/> or throws an exception.
    /// </summary>
    /// <typeparam name="TValueObject">The value object or target domain type.</typeparam>
    /// <param name="factory">Factory delegate (e.g. <c>val =&gt; Email.Create(val)</c>).</param>
    /// <param name="code">Stable error code.</param>
    /// <param name="messageTemplate">User-facing error message.</param>
    /// <param name="severity">Validation severity.</param>
    public RuleBuilder<T, TProperty> MustCreate<TValueObject>(
        Func<TProperty, TValueObject?> factory,
        string code = ValidationCodes.Predicate,
        string messageTemplate = "{Property} is invalid.",
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (factory is null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        var check = new ValidationCheck<T, TProperty>(
            (_, value) =>
            {
                if (value is null)
                {
                    return true;
                }

                try
                {
                    var created = factory(value);
                    return created is not null;
                }
                catch
                {
                    return false;
                }
            },
            null,
            code,
            messageTemplate)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }

    /// <summary>
    /// Validates that the property value can successfully instantiate a domain Value Object via a try-factory or predicate.
    /// </summary>
    /// <param name="tryFactory">Try-factory delegate returning a boolean success indicator (e.g. <c>val =&gt; Email.TryCreate(val, out _)</c>).</param>
    /// <param name="code">Stable error code.</param>
    /// <param name="messageTemplate">User-facing error message.</param>
    /// <param name="severity">Validation severity.</param>
    public RuleBuilder<T, TProperty> MustCreate(
        Func<TProperty, bool> tryFactory,
        string code = ValidationCodes.Predicate,
        string messageTemplate = "{Property} is invalid.",
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (tryFactory is null)
        {
            throw new ArgumentNullException(nameof(tryFactory));
        }

        var check = new ValidationCheck<T, TProperty>(
            (_, value) =>
            {
                if (value is null)
                {
                    return true;
                }

                try
                {
                    return tryFactory(value);
                }
                catch
                {
                    return false;
                }
            },
            null,
            code,
            messageTemplate)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }

#if NET7_0_OR_GREATER
    /// <summary>
    /// Validates that a string property can be parsed into <typeparamref name="TParsable"/> using <see cref="IParsable{TSelf}"/>.
    /// </summary>
    /// <typeparam name="TParsable">The target type implementing <see cref="IParsable{TSelf}"/>.</typeparam>
    /// <param name="code">Stable error code.</param>
    /// <param name="messageTemplate">User-facing error message.</param>
    /// <param name="formatProvider">Optional format provider.</param>
    /// <param name="severity">Validation severity.</param>
    public RuleBuilder<T, TProperty> MustParse<TParsable>(
        string code = ValidationCodes.Predicate,
        string messageTemplate = "{Property} is invalid.",
        IFormatProvider? formatProvider = null,
        ValidationSeverity severity = ValidationSeverity.Error)
        where TParsable : IParsable<TParsable>
    {
        var check = new ValidationCheck<T, TProperty>(
            (_, value) =>
            {
                if (value is null)
                {
                    return true;
                }

                if (value is string text)
                {
                    return string.IsNullOrWhiteSpace(text) || TParsable.TryParse(text, formatProvider ?? System.Globalization.CultureInfo.InvariantCulture, out TParsable? _);
                }

                return true;
            },
            null,
            code,
            messageTemplate)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }
#endif

    /// <summary>
    /// Adds a pattern-matching condition for relational or cross-field validation.
    /// The rule FAILS if <paramref name="patternCondition"/> returns <see langword="true"/> (the invalid pattern was matched).
    /// </summary>
    /// <param name="patternCondition">Predicate identifying the invalid condition (e.g. <c>x =&gt; x is { IsVip: true, CreditLimit: &lt; 5000 }</c>).</param>
    /// <param name="code">Stable error code.</param>
    /// <param name="messageTemplate">User-facing error message.</param>
    /// <param name="severity">Validation severity.</param>
    public RuleBuilder<T, TProperty> Match(
        Func<T, bool> patternCondition,
        string code,
        string messageTemplate,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (patternCondition is null)
        {
            throw new ArgumentNullException(nameof(patternCondition));
        }

        var check = new ValidationCheck<T, TProperty>(
            (instance, _) => !patternCondition(instance),
            null,
            code,
            messageTemplate)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }

    /// <summary>
    /// Adds a pattern-matching condition targeting a specific property path for error reporting.
    /// The rule FAILS if <paramref name="patternCondition"/> returns <see langword="true"/> (the invalid pattern was matched).
    /// </summary>
    /// <param name="patternCondition">Predicate identifying the invalid condition.</param>
    /// <param name="targetPropertyPath">Property path to attach the error to.</param>
    /// <param name="code">Stable error code.</param>
    /// <param name="messageTemplate">User-facing error message.</param>
    /// <param name="severity">Validation severity.</param>
    public RuleBuilder<T, TProperty> Match(
        Func<T, bool> patternCondition,
        string targetPropertyPath,
        string code,
        string messageTemplate,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (patternCondition is null)
        {
            throw new ArgumentNullException(nameof(patternCondition));
        }

        if (string.IsNullOrWhiteSpace(targetPropertyPath))
        {
            throw new ArgumentException("A target property path is required.", nameof(targetPropertyPath));
        }

        var check = new ValidationCheck<T, TProperty>(
            (instance, _) => !patternCondition(instance),
            null,
            code,
            messageTemplate,
            targetPath: targetPropertyPath)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }

    /// <summary>
    /// Adds an asynchronous pattern-matching condition for relational or cross-field validation.
    /// The rule FAILS if <paramref name="asyncPatternCondition"/> returns <see langword="true"/> (the invalid pattern was matched).
    /// </summary>
    public RuleBuilder<T, TProperty> MatchAsync(
        Func<T, ValidationContext, CancellationToken, Task<bool>> asyncPatternCondition,
        string code,
        string messageTemplate,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (asyncPatternCondition is null)
        {
            throw new ArgumentNullException(nameof(asyncPatternCondition));
        }

        var check = new ValidationCheck<T, TProperty>(
            null,
            async (instance, _, context, ct) => !await asyncPatternCondition(instance, context, ct).ConfigureAwait(false),
            code,
            messageTemplate)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }

    /// <summary>
    /// Adds an asynchronous pattern-matching condition targeting a specific property path for error reporting.
    /// The rule FAILS if <paramref name="asyncPatternCondition"/> returns <see langword="true"/> (the invalid pattern was matched).
    /// </summary>
    public RuleBuilder<T, TProperty> MatchAsync(
        Func<T, ValidationContext, CancellationToken, Task<bool>> asyncPatternCondition,
        string targetPropertyPath,
        string code,
        string messageTemplate,
        ValidationSeverity severity = ValidationSeverity.Error)
    {
        if (asyncPatternCondition is null)
        {
            throw new ArgumentNullException(nameof(asyncPatternCondition));
        }

        if (string.IsNullOrWhiteSpace(targetPropertyPath))
        {
            throw new ArgumentException("A target property path is required.", nameof(targetPropertyPath));
        }

        var check = new ValidationCheck<T, TProperty>(
            null,
            async (instance, _, context, ct) => !await asyncPatternCondition(instance, context, ct).ConfigureAwait(false),
            code,
            messageTemplate,
            targetPath: targetPropertyPath)
        {
            Severity = severity
        };

        _rule.Add(check);
        return this;
    }

    /// <summary>Replaces the message for the immediately preceding validator.</summary>
    public RuleBuilder<T, TProperty> WithMessage(string messageTemplate)
    {
        if (string.IsNullOrWhiteSpace(messageTemplate))
        {
            throw new ArgumentException("A validation message is required.", nameof(messageTemplate));
        }

        GetLastCheck().Template = messageTemplate;
        return this;
    }

    /// <summary>Replaces the stable error code for the immediately preceding validator.</summary>
    public RuleBuilder<T, TProperty> WithCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A validation code is required.", nameof(code));
        }

        GetLastCheck().Code = code;
        return this;
    }

    /// <summary>Changes the severity for the immediately preceding validator.</summary>
    public RuleBuilder<T, TProperty> WithSeverity(ValidationSeverity severity)
    {
        GetLastCheck().Severity = severity;
        return this;
    }

    /// <summary>Changes the display name used by every default message in this rule chain.</summary>
    public RuleBuilder<T, TProperty> WithName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("A display name is required.", nameof(displayName));
        }

        _rule.DisplayName = displayName;
        return this;
    }

    /// <summary>Runs this property rule only when the model condition is true.</summary>
    public RuleBuilder<T, TProperty> When(Func<T, bool> condition)
    {
        if (condition is null)
        {
            throw new ArgumentNullException(nameof(condition));
        }

        _rule.SetCondition((instance, _) => condition(instance));
        return this;
    }

    /// <summary>Runs this property rule only for the supplied validation scenarios.</summary>
    public RuleBuilder<T, TProperty> ForScenarios(params string[] scenarios)
    {
        if (scenarios is null || scenarios.Length == 0)
        {
            throw new ArgumentException("At least one scenario is required.", nameof(scenarios));
        }

        _rule.AddScenarios(scenarios);
        return this;
    }

    /// <summary>Stops evaluating this property's rule chain after its first failure.</summary>
    public RuleBuilder<T, TProperty> StopOnFirstFailure()
    {
        _rule.StopOnFirstFailure = true;
        return this;
    }

    /// <summary>Validates a non-null nested object with another validator.</summary>
    public RuleBuilder<T, TProperty> SetValidator(IValidator<TProperty> validator)
    {
        _rule.SetChildValidator(validator);
        return this;
    }

    internal RuleBuilder<T, TProperty> Add(
        Func<T, TProperty, bool> predicate,
        string code,
        string template,
        IReadOnlyDictionary<string, object?>? arguments = null)
    {
        _rule.Add(new ValidationCheck<T, TProperty>(predicate, null, code, template, arguments));
        return this;
    }

    private ValidationCheck<T, TProperty> GetLastCheck() =>
        _rule.LastCheck ?? throw new InvalidOperationException("Configure a validator before configuring its message, code or severity.");

    private static bool IsEmpty(TProperty value)
    {
        if (value is null)
        {
            return true;
        }

        if (value is string text)
        {
            return text.Length == 0;
        }

        if (value is ICollection collection)
        {
            return collection.Count == 0;
        }

        if (value is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            try
            {
                return !enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        return EqualityComparer<TProperty>.Default.Equals(value, default!);
    }
}
