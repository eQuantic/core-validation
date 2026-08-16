namespace eQuantic.Validation;

/// <summary>
/// Covariant handle for a property rule chain, implemented by <see cref="RuleBuilder{TModel, TProperty}"/>.
/// Covariance lets type-safe rule extensions bind at compile time: a builder for a non-nullable
/// <c>string</c> property converts to <c>IRuleBuilder&lt;TModel, string?&gt;</c> without nullability
/// warnings, while non-string properties fail to bind at all.
/// </summary>
/// <typeparam name="TModel">The model being validated.</typeparam>
/// <typeparam name="TProperty">The property type.</typeparam>
public interface IRuleBuilder<TModel, out TProperty>
{
}
