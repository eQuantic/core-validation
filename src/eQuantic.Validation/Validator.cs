using System.Linq.Expressions;
using eQuantic.Validation.Internal;

namespace eQuantic.Validation;

/// <summary>
/// Base class for code-first validators. Rules are declared once in the constructor and the
/// validator instance is safe to reuse when its own dependencies are thread-safe.
/// </summary>
/// <typeparam name="T">The model to validate.</typeparam>
public abstract class Validator<T> : IValidator<T>, IDescribableValidator
{
    private readonly List<IValidationRule<T>> _rules = new();

    /// <inheritdoc />
    public Type ValidatedType => typeof(T);

    /// <summary>Returns every rule of this validator as data, including composed and nested rules.</summary>
    public ValidatorDescription Describe() =>
        new(typeof(T), _rules.SelectMany(static rule => rule.Describe()).ToArray());

    /// <summary>Begins a rule chain for a property or nested property.</summary>
    protected RuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> expression)
    {
        var rule = new PropertyRule<T, TProperty>(PropertyPath.FromExpression(expression), expression.Compile());
        _rules.Add(rule);
        return new RuleBuilder<T, TProperty>(rule);
    }

    /// <summary>Begins a rule chain for the complete model instance, ideal for relational and pattern-matching rules.</summary>
    protected RuleBuilder<T, T> RuleForModel() => RuleFor(static instance => instance);

    /// <summary>Begins a rule chain that runs once for each item in a collection.</summary>
    protected CollectionRuleBuilder<T, TElement> RuleForEach<TElement>(
        Expression<Func<T, IEnumerable<TElement>?>> expression)
    {
        var rule = new CollectionRule<T, TElement>(PropertyPath.FromExpression(expression), expression.Compile());
        _rules.Add(rule);
        return new CollectionRuleBuilder<T, TElement>(rule);
    }

    /// <summary>Composes every rule from another validator for the same model.</summary>
    protected void Include(IValidator<T> validator)
    {
        _rules.Add(new DelegatingRule<T>(validator ?? throw new ArgumentNullException(nameof(validator))));
    }

    /// <inheritdoc />
    public ValidationResult Validate(T instance, ValidationContext? context = null)
    {
        var effectiveContext = context ?? ValidationContext.Default;
        var failures = new List<ValidationFailure>();

        foreach (var rule in _rules)
        {
            failures.AddRange(rule.Validate(instance, effectiveContext));
        }

        return failures.Count == 0 ? ValidationResult.Success : new ValidationResult(failures);
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAsync(
        T instance,
        ValidationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveContext = context ?? ValidationContext.Default;
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<ValidationFailure>[] results;
        if (effectiveContext.ExecutionMode == ValidationExecutionMode.Parallel)
        {
            var tasks = _rules
                .Select(rule => rule.ValidateAsync(instance, effectiveContext, cancellationToken))
                .ToArray();
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        else
        {
            results = new IReadOnlyList<ValidationFailure>[_rules.Count];
            for (var index = 0; index < _rules.Count; index++)
            {
                results[index] = await _rules[index]
                    .ValidateAsync(instance, effectiveContext, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var failures = results.SelectMany(static result => result).ToArray();
        return failures.Length == 0 ? ValidationResult.Success : new ValidationResult(failures);
    }

    ValidationResult IValidator.Validate(object instance, ValidationContext? context)
    {
        return Validate(Cast(instance), context);
    }

    Task<ValidationResult> IValidator.ValidateAsync(
        object instance,
        ValidationContext? context,
        CancellationToken cancellationToken)
    {
        return ValidateAsync(Cast(instance), context, cancellationToken);
    }

    private static T Cast(object instance)
    {
        if (instance is T typed)
        {
            return typed;
        }

        throw new ArgumentException(
            $"Expected an instance of '{typeof(T).FullName}' but received '{instance?.GetType().FullName ?? "null"}'.",
            nameof(instance));
    }
}
