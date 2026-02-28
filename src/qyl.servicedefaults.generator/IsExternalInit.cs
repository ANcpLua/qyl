// Polyfills required for language features on netstandard2.0.
// These must live in a separate file because file-scoped namespaces cannot follow block namespaces.
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class NotNullWhenAttribute(bool returnValue) : Attribute
    {
        public bool ReturnValue { get; } = returnValue;
    }
}

namespace System
{
    internal readonly struct Index
    {
        private readonly int _value;
        public Index(int value, bool fromEnd = false) => _value = fromEnd ? ~value : value;
        public static Index End => new Index(0, fromEnd: true);
        public int GetOffset(int length) => _value < 0 ? length + _value + 1 : _value;
        public static implicit operator Index(int value) => new Index(value);
    }

    internal readonly struct Range
    {
        public Index Start { get; }
        public Index End { get; }
        public Range(Index start, Index end) { Start = start; End = end; }
        public static Range EndAt(Index end) => new Range(new Index(0), end);
        public (int Offset, int Length) GetOffsetAndLength(int length)
        {
            var s = Start.GetOffset(length);
            var e = End.GetOffset(length);
            return (s, e - s);
        }
    }
}
