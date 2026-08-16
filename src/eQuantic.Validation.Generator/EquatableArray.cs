using System.Collections;

namespace eQuantic.Validation.Generator;

/// <summary>
/// An immutable array with value equality, required for incremental generator pipeline caching:
/// descriptor models must compare by value or every keystroke invalidates the cache.
/// </summary>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new(Array.Empty<T>());

    private readonly T[]? _items;

    public EquatableArray(T[] items) => _items = items;

    public int Count => _items?.Length ?? 0;

    public T this[int index] => (_items ?? Array.Empty<T>())[index];

    public bool Equals(EquatableArray<T> other)
    {
        var left = _items ?? Array.Empty<T>();
        var right = other._items ?? Array.Empty<T>();
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var items = _items ?? Array.Empty<T>();
        unchecked
        {
            var hash = 17;
            foreach (var item in items)
            {
                hash = hash * 31 + item.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

