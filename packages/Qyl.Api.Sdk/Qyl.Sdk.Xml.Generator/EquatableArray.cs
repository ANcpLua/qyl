using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Qyl.Sdk.Xml.Generator;

/// <summary>An immutable array with structural equality, so pipeline models stay cacheable across incremental runs.</summary>
internal readonly struct EquatableArray<T>(ImmutableArray<T> array) : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array = array.IsDefault ? ImmutableArray<T>.Empty : array;

    public int Count => _array.Length;

    public T this[int index] => _array[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.Length != other._array.Length)
        {
            return false;
        }

        for (var index = 0; index < _array.Length; index++)
        {
            if (!_array[index].Equals(other._array[index]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            foreach (var item in _array)
            {
                hash = (hash * 31) + item.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_array).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
