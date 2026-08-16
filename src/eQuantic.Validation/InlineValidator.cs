using System.Linq.Expressions;

namespace eQuantic.Validation;

/// <summary>
/// A validator whose rules are declared from the outside — no subclass required. Handy for
/// tests, quick composition and collection elements:
/// <code>
/// var validator = new InlineValidator&lt;Customer&gt;();
/// validator.RuleFor(x =&gt; x.Email).NotWhiteSpace().Email();
/// </code>
/// </summary>
/// <typeparam name="T">The model to validate.</typeparam>
public sealed class InlineValidator<T> : Validator<T>
{
    /// <summary>Initializes an empty inline validator; declare rules with <see cref="RuleFor{TProperty}"/>.</summary>
    public InlineValidator()
    {
    }

    /// <summary>Initializes the validator and declares rules through <paramref name="setup"/>.</summary>
    public InlineValidator(Action<InlineValidator<T>> setup)
    {
        if (setup is null)
        {
            throw new ArgumentNullException(nameof(setup));
        }

        setup(this);
    }

    /// <summary>Begins a rule chain for a property or nested property.</summary>
    public new RuleBuilder<T, TProperty> RuleFor<TProperty>(Expression<Func<T, TProperty>> expression) =>
        base.RuleFor(expression);

    /// <summary>Begins a rule chain for the complete model instance.</summary>
    public new RuleBuilder<T, T> RuleForModel() => base.RuleForModel();

    /// <summary>Begins a rule chain that runs once for each item in a collection.</summary>
    public new CollectionRuleBuilder<T, TElement> RuleForEach<TElement>(
        Expression<Func<T, IEnumerable<TElement>?>> expression) => base.RuleForEach(expression);

    /// <summary>Composes every rule from another validator for the same model.</summary>
    public new void Include(IValidator<T> validator) => base.Include(validator);
}
