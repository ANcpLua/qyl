// =============================================================================
// qyl Shared - Throw Helpers
// Injected via <InjectSharedThrow>true</InjectSharedThrow>
// =============================================================================

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Qyl;

/// <summary>
/// Provides helper methods for argument validation that throw on failure.
/// </summary>
internal static class Throw
{
    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> if <paramref name="argument"/> is null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNull<T>(
        [NotNull] T? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(argument, paramName);
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="argument"/> is null or whitespace.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNullOrWhiteSpace(
        [NotNull] string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument, paramName);
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNegative(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is zero or negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNegativeOrZero(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is less than <paramref name="other"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfLessThan<T>(
        T value,
        T other,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : IComparable<T>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, other, paramName);
    }
}
