using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System;

/// <summary>
///     Backport of <c>Index</c> for .NET Standard 2.0 and .NET Framework.
/// </summary>
/// <remarks>
///     <para>
///         This type is only defined on older target frameworks and is automatically
///         available on .NET Core 3.0+ and .NET Standard 2.1+ through the standard library.
///     </para>
///     <para>
///         An <c>Index</c> represents a position in a collection, either from the start
///         (non-negative) or from the end (using the <c>^</c> operator in C#). This enables
///         the range and index syntax: <c>array[^1]</c> for the last element.
///     </para>
///     <para>
///         Internally, the index uses a compact representation where negative values
///         (via bitwise complement) indicate "from end" indices.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     var array = new[] { 1, 2, 3, 4, 5 };
///     var last = array[^1];     // 5 (last element)
///     var secondLast = array[^2]; // 4 (second to last)
///     var first = array[0];     // 1 (first element)
///     </code>
/// </example>
[ExcludeFromCodeCoverage]
internal readonly struct Index : IEquatable<Index>
{
    private readonly int _value;

    /// <summary>
    ///     Initializes a new instance of the <c>Index</c> struct.
    /// </summary>
    /// <param name="value">
    ///     The index value. Must be non-negative.
    /// </param>
    /// <param name="fromEnd">
    ///     If <c>true</c>, the index counts from the end of the collection (e.g., <c>^1</c> is the last element).
    ///     If <c>false</c> (default), the index counts from the start (e.g., <c>0</c> is the first element).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="value" /> is negative.
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         When <paramref name="fromEnd" /> is <c>true</c>, a value of <c>1</c> refers to the last element,
    ///         <c>2</c> to the second-to-last, and so on. A value of <c>0</c> with <paramref name="fromEnd" />
    ///         set to <c>true</c> represents the position immediately after the last element.
    ///     </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Index(int value, bool fromEnd = false)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Non-negative number required.");

        _value = fromEnd ? ~value : value;
    }

    private Index(int value) => _value = value;

    /// <summary>
    ///     Gets an <c>Index</c> pointing to the first element (index 0).
    /// </summary>
    public static Index Start => new(0);

    /// <summary>
    ///     Gets an <c>Index</c> pointing to the position after the last element (<c>^0</c>).
    /// </summary>
    public static Index End => new(~0);

    /// <summary>
    ///     Creates an <c>Index</c> from the start of a collection.
    /// </summary>
    /// <param name="value">The zero-based index from the start. Must be non-negative.</param>
    /// <returns>An <c>Index</c> representing the specified position from the start.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="value" /> is negative.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Index FromStart(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Non-negative number required.");
        return new Index(value);
    }

    /// <summary>
    ///     Creates an <c>Index</c> from the end of a collection.
    /// </summary>
    /// <param name="value">
    ///     The index from the end. A value of <c>1</c> refers to the last element,
    ///     <c>2</c> to the second-to-last, and so on. Must be non-negative.
    /// </param>
    /// <returns>An <c>Index</c> representing the specified position from the end.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="value" /> is negative.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Index FromEnd(int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Non-negative number required.");
        return new Index(~value);
    }

    /// <summary>
    ///     Gets the index value. For "from end" indices, this is the distance from the end.
    /// </summary>
    public int Value => _value < 0 ? ~_value : _value;

    /// <summary>
    ///     Gets a value indicating whether this index counts from the end of the collection.
    /// </summary>
    /// <value>
    ///     <c>true</c> if this index was created with <c>fromEnd: true</c> or via <see cref="FromEnd" />;
    ///     otherwise, <c>false</c>.
    /// </value>
    public bool IsFromEnd => _value < 0;

    /// <summary>
    ///     Calculates the actual offset from the start of a collection of the specified length.
    /// </summary>
    /// <param name="length">The total length of the collection being indexed.</param>
    /// <returns>
    ///     The zero-based offset from the start of the collection.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         For a "from start" index, this simply returns the <see cref="Value" />.
    ///         For a "from end" index, this computes <c>length - Value</c>.
    ///     </para>
    ///     <para>
    ///         Note: This method does not validate that the resulting offset is within bounds.
    ///         The caller is responsible for bounds checking.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var fromStart = new Index(2);       // index 2
    ///     var fromEnd = new Index(1, true);   // ^1 (last element)
    ///
    ///     int length = 5;
    ///     fromStart.GetOffset(length); // returns 2
    ///     fromEnd.GetOffset(length);   // returns 4 (5 - 1)
    ///     </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetOffset(int length)
    {
        var offset = _value;
        if (IsFromEnd) offset += length + 1;
        return offset;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Index other && _value == other._value;

    /// <inheritdoc />
    public bool Equals(Index other) => _value == other._value;

    /// <inheritdoc />
    public override int GetHashCode() => _value;

    /// <summary>
    ///     Implicitly converts an <see cref="int" /> to an <c>Index</c> from the start.
    /// </summary>
    /// <param name="value">The integer index value.</param>
    /// <returns>An <c>Index</c> equivalent to <c>Index.FromStart(value)</c>.</returns>
    public static implicit operator Index(int value) => FromStart(value);

    /// <summary>
    ///     Returns a string representation of this index.
    /// </summary>
    /// <returns>
    ///     For "from end" indices, returns <c>^N</c> (e.g., "^1").
    ///     For "from start" indices, returns the numeric value (e.g., "0").
    /// </returns>
    public override string ToString() => IsFromEnd ? "^" + Value : ((uint)Value).ToString();
}
#endif
