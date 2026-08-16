using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace eQuantic.Validation;

/// <summary>
/// Deduplicates asynchronous rule evaluations within one validation operation. When the same
/// rule is evaluated again for the same model instance and value — composed validators, repeated
/// collection elements, revalidation in the same request — the first task is reused instead of
/// repeating the I/O. Scoped to a <see cref="ValidationContext"/>; never share across requests.
/// </summary>
public sealed class AsyncRuleCache
{
    private readonly ConcurrentDictionary<CacheKey, Task<bool>> _entries = new();

    /// <summary>
    /// Returns the cached evaluation for (<paramref name="ruleIdentity"/>, <paramref name="instance"/>,
    /// <paramref name="value"/>) or runs <paramref name="evaluate"/> and caches its task.
    /// </summary>
    public Task<bool> GetOrAdd(object ruleIdentity, object? instance, object? value, Func<Task<bool>> evaluate)
    {
        if (ruleIdentity is null)
        {
            throw new ArgumentNullException(nameof(ruleIdentity));
        }

        if (evaluate is null)
        {
            throw new ArgumentNullException(nameof(evaluate));
        }

        return _entries.GetOrAdd(new CacheKey(ruleIdentity, instance, value), _ => evaluate());
    }

    private readonly struct CacheKey : IEquatable<CacheKey>
    {
        private readonly object _rule;
        private readonly object? _instance;
        private readonly object? _value;

        public CacheKey(object rule, object? instance, object? value)
        {
            _rule = rule;
            _instance = instance;
            _value = value;
        }

        public bool Equals(CacheKey other) =>
            ReferenceEquals(_rule, other._rule) &&
            ReferenceEquals(_instance, other._instance) &&
            Equals(_value, other._value);

        public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RuntimeHelpers.GetHashCode(_rule);
                hash = hash * 31 + (_instance is null ? 0 : RuntimeHelpers.GetHashCode(_instance));
                hash = hash * 31 + (_value?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}
