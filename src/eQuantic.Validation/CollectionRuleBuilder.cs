using eQuantic.Validation.Internal;

namespace eQuantic.Validation;

/// <summary>Fluent builder for a rule applied to every element in a collection.</summary>
public sealed class CollectionRuleBuilder<T, TElement>
{
    private readonly CollectionRule<T, TElement> _rule;

    internal CollectionRuleBuilder(CollectionRule<T, TElement> rule) => _rule = rule;

    /// <summary>Requires every element to pass a synchronous predicate.</summary>
    public CollectionRuleBuilder<T, TElement> Must(
        Func<TElement, bool> predicate,
        string code = ValidationCodes.Predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        _rule.Add(new ValidationCheck<T, TElement>((_, element) => predicate(element), null, code, "{Property} is invalid."));
        return this;
    }

    /// <summary>Requires every element to pass an asynchronous, cancellation-aware predicate.</summary>
    public CollectionRuleBuilder<T, TElement> MustAsync(
        Func<T, TElement, ValidationContext, CancellationToken, Task<bool>> predicate,
        string code = ValidationCodes.Predicate)
    {
        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        _rule.Add(new ValidationCheck<T, TElement>(null, predicate, code, "{Property} is invalid."));
        return this;
    }

    /// <summary>Validates every non-null element using another validator.</summary>
    public CollectionRuleBuilder<T, TElement> SetValidator(IValidator<TElement> validator)
    {
        _rule.SetChildValidator(validator);
        return this;
    }

    /// <summary>Replaces the message for the immediately preceding element validator.</summary>
    public CollectionRuleBuilder<T, TElement> WithMessage(string messageTemplate)
    {
        if (string.IsNullOrWhiteSpace(messageTemplate))
        {
            throw new ArgumentException("A validation message is required.", nameof(messageTemplate));
        }

        GetLastCheck().Template = messageTemplate;
        return this;
    }

    /// <summary>Replaces the code for the immediately preceding element validator.</summary>
    public CollectionRuleBuilder<T, TElement> WithCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A validation code is required.", nameof(code));
        }

        GetLastCheck().Code = code;
        return this;
    }

    /// <summary>Runs the collection rule only for the supplied scenarios.</summary>
    public CollectionRuleBuilder<T, TElement> ForScenarios(params string[] scenarios)
    {
        if (scenarios is null || scenarios.Length == 0)
        {
            throw new ArgumentException("At least one scenario is required.", nameof(scenarios));
        }

        _rule.AddScenarios(scenarios);
        return this;
    }

    private ValidationCheck<T, TElement> GetLastCheck() =>
        _rule.LastCheck ?? throw new InvalidOperationException("Configure an element validator before configuring its message or code.");
}
