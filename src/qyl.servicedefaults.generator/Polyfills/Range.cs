using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#if !NETCOREAPP3_0_OR_GREATER && !NETSTANDARD2_1_OR_GREATER
namespace System;

/// <summary>
///     Backport of <see cref="Range" /> for .NET Standard 2.0 and .NET Framework.
/// </summary>
/// <remarks>
///     <para>
///         This type is only defined on older target frameworks and is automatically
///         available on .NET Core 3.0+ and .NET Standard 2.1+ through the standard library.
///     </para>
///     <para>
///         A <see cref="Range" /> represents a contiguous region within a collection, defined by
///         a start and end <see cref="Index" />. This enables the C# range syntax: <c>array[1..^1]</c>
///         to get all elements except the first and last.
///     </para>
///     <para>
///         Ranges are end-exclusive, meaning the end index is not included in the range.
///         For example, <c>0..3</c> includes elements at indices 0, 1, and 2.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     var array = new[] { 0, 1, 2, 3, 4 };
///     var slice = array[1..4];    // { 1, 2, 3 }
///     var tail = array[2..];      // { 2, 3, 4 }
///     var head = array[..3];      // { 0, 1, 2 }
///     var middle = array[1..^1];  // { 1, 2, 3 }
///     </code>
/// </example>
[ExcludeFromCodeCoverage]
internal readonly struct Range : IEquatable<Range>
{
    /// <summary>
    ///     Gets the inclusive start index of the range.
    /// </summary>
    public Index Start { get; }

    /// <summary>
    ///     Gets the exclusive end index of the range.
    /// </summary>
    /// <remarks>
    ///     The element at the end index is not included in the range.
    ///     For example, in <c>0..3</c>, elements at indices 0, 1, and 2 are included, but not 3.
    /// </remarks>
    public Index End { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Range" /> struct with the specified start and end indices.
    /// </summary>
    /// <param name="start">The inclusive start index of the range.</param>
    /// <param name="end">The exclusive end index of the range.</param>
    /// <remarks>
    ///     <para>
    ///         Both indices can be "from start" or "from end" indices. For example:
    ///         <list type="bullet">
    ///             <item>
    ///                 <description><c>new Range(1, 3)</c> - elements at indices 1 and 2</description>
    ///             </item>
    ///             <item>
    ///                 <description>
    ///                     <c>new Range(Index.FromEnd(3), Index.FromEnd(1))</c> - third-to-last through
    ///                     second-to-last
    ///                 </description>
    ///             </item>
    ///         </list>
    ///     </para>
    /// </remarks>
    public Range(Index start, Index end)
    {
        Start = start;
        End = end;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Range other && other.Start.Equals(Start) && other.End.Equals(End);

    /// <inheritdoc />
    public bool Equals(Range other) => other.Start.Equals(Start) && other.End.Equals(End);

    /// <inheritdoc />
    public override int GetHashCode() => Start.GetHashCode() * 31 + End.GetHashCode();

    /// <summary>
    ///     Returns the string representation of this range in C# syntax.
    /// </summary>
    /// <returns>
    ///     A string in the format <c>start..end</c> (e.g., "0..5", "^3..^1").
    /// </returns>
    public override string ToString() => Start + ".." + End;

    /// <summary>
    ///     Creates a range starting at the specified index and ending at the end of the collection.
    /// </summary>
    /// <param name="start">The inclusive start index.</param>
    /// <returns>A <see cref="Range" /> equivalent to <c>start..</c> in C# syntax.</returns>
    public static Range StartAt(Index start) => new(start, Index.End);

    /// <summary>
    ///     Creates a range starting at the beginning and ending at the specified index.
    /// </summary>
    /// <param name="end">The exclusive end index.</param>
    /// <returns>A <see cref="Range" /> equivalent to <c>..end</c> in C# syntax.</returns>
    public static Range EndAt(Index end) => new(Index.Start, end);

    /// <summary>
    ///     Gets a range representing the entire collection.
    /// </summary>
    /// <value>A <see cref="Range" /> equivalent to <c>..</c> in C# syntax (from start to end).</value>
    public static Range All => new(Index.Start, Index.End);

    /// <summary>
    ///     Calculates the start offset and length of this range for a collection of the specified length.
    /// </summary>
    /// <param name="length">The total length of the collection.</param>
    /// <returns>
    ///     A tuple containing:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><c>Offset</c> - The zero-based starting position in the collection.</description>
    ///         </item>
    ///         <item>
    ///             <description><c>Length</c> - The number of elements in the range.</description>
    ///         </item>
    ///     </list>
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when the calculated range is outside the bounds of the collection
    ///     (i.e., end &gt; length, or start &gt; end).
    /// </exception>
    /// <remarks>
    ///     <para>
    ///         This method converts both "from start" and "from end" indices to absolute offsets,
    ///         then validates that the resulting range is valid for the collection.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    ///     var range = 1..^1;  // Skip first and last
    ///     var (offset, len) = range.GetOffsetAndLength(5);
    ///     // offset = 1, len = 3 (elements at indices 1, 2, 3)
    ///     </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int Offset, int Length) GetOffsetAndLength(int length)
    {
        var start = Start.GetOffset(length);
        var end = End.GetOffset(length);
        if ((uint)end > (uint)length || (uint)start > (uint)end) throw new ArgumentOutOfRangeException(nameof(length));
        return (start, end - start);
    }
}
#endif
