using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ANcpLua.Roslyn.Utilities;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>
{
    private readonly ImmutableArray<T> _items;

    public EquatableArray(ImmutableArray<T> items)
    {
        _items = items.IsDefault ? ImmutableArray<T>.Empty : items;
    }

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public bool IsEmpty => _items.IsDefaultOrEmpty;

    public int Length => _items.IsDefault ? 0 : _items.Length;

    public ImmutableArray<T> AsImmutableArray() => _items.IsDefault ? ImmutableArray<T>.Empty : _items;

    public bool Equals(EquatableArray<T> other)
    {
        var left = AsImmutableArray();
        var right = other.AsImmutableArray();
        if (left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        var items = AsImmutableArray();
        foreach (var item in items)
            hash = unchecked(hash * 31 + (item?.GetHashCode() ?? 0));
        return hash;
    }
}

internal static class IncrementalValuesProviderExtensions
{
    public static IncrementalValuesProvider<T> WhereNotNull<T>(
        this IncrementalValuesProvider<T?> provider)
        where T : class
    {
        return provider
            .Where(static value => value is not null)
            .Select(static (value, _) => value!);
    }

    public static IncrementalValuesProvider<T> WhereNotNull<T>(
        this IncrementalValuesProvider<T?> provider)
        where T : struct
    {
        return provider
            .Where(static value => value.HasValue)
            .Select(static (value, _) => value!.Value);
    }

    public static IncrementalValueProvider<EquatableArray<T>> CollectAsEquatableArray<T>(
        this IncrementalValuesProvider<T> provider)
    {
        return provider
            .Collect()
            .Select(static (items, _) => new EquatableArray<T>(items));
    }

    public static IncrementalValueProvider<(TLeft Left, TRight Right)> CombineWith<TLeft, TRight>(
        this IncrementalValueProvider<TLeft> left,
        IncrementalValueProvider<TRight> right)
    {
        return left.Combine(right);
    }
}
