using System.Collections.Concurrent;

namespace eQuantic.Validation;

/// <summary>
/// Registry of compiled property accessors, populated at module load by the source generator.
/// When an accessor is registered for a (model, path) pair, <c>RuleFor</c> uses it directly and
/// skips <c>Expression.Compile()</c> — removing per-instantiation expression compilation on JIT
/// and the expression-interpreter cost on Native AOT. Unregistered paths fall back to compiling
/// the expression, so behavior never changes.
/// </summary>
public static class ValidatorAccessors
{
    private static readonly ConcurrentDictionary<(Type ModelType, string Path, Type ValueType), Delegate> Accessors = new();

    /// <summary>Registers a compiled accessor for a member path of <typeparamref name="TModel"/>. Idempotent.</summary>
    public static void Register<TModel, TValue>(string path, Func<TModel, TValue> accessor)
    {
        if (path is null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (accessor is null)
        {
            throw new ArgumentNullException(nameof(accessor));
        }

        Accessors.TryAdd((typeof(TModel), path, typeof(TValue)), accessor);
    }

    internal static Func<TModel, TValue>? Find<TModel, TValue>(string path) =>
        Accessors.TryGetValue((typeof(TModel), path, typeof(TValue)), out var accessor)
            ? (Func<TModel, TValue>)accessor
            : null;
}
