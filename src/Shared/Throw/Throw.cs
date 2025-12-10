// Copyright (c) qyl. All rights reserved.
// Shared argument validation helpers.

#pragma warning disable IDE0005 // Using directive is unnecessary.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace qyl.providers.gemini.Throw;

/// <summary>
/// Defines static methods used to throw exceptions with standardized messages.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class Throw
{
    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the specified argument is <see langword="null"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static T IfNull<T>([NotNull] T argument, [CallerArgumentExpression(nameof(argument))] string paramName = "")
    {
        if (argument is null)
        {
            ThrowArgumentNullException(paramName);
        }
        return argument;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the string is <see langword="null"/>,
    /// or <see cref="ArgumentException"/> if it is empty or whitespace.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static string IfNullOrWhitespace([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string paramName = "")
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            if (argument is null)
            {
                ThrowArgumentNullException(paramName);
            }
            else
            {
                ThrowArgumentException(paramName, "Argument is empty or whitespace");
            }
        }
        return argument;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/> if the string is <see langword="null"/>,
    /// or <see cref="ArgumentException"/> if it is empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static string IfNullOrEmpty([NotNull] string? argument, [CallerArgumentExpression(nameof(argument))] string paramName = "")
    {
        if (string.IsNullOrEmpty(argument))
        {
            if (argument is null)
            {
                ThrowArgumentNullException(paramName);
            }
            else
            {
                ThrowArgumentException(paramName, "Argument is an empty string");
            }
        }
        return argument;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified number is less than min.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IfLessThan(int argument, int min, [CallerArgumentExpression(nameof(argument))] string paramName = "")
    {
        if (argument < min)
        {
            ThrowArgumentOutOfRangeException(paramName, $"Argument less than minimum value {min}");
        }
        return argument;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified number is greater than max.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IfGreaterThan(int argument, int max, [CallerArgumentExpression(nameof(argument))] string paramName = "")
    {
        if (argument > max)
        {
            ThrowArgumentOutOfRangeException(paramName, $"Argument greater than maximum value {max}");
        }
        return argument;
    }

    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> if the specified number is not in the specified range.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IfOutOfRange(int argument, int min, int max, [CallerArgumentExpression(nameof(argument))] string paramName = "")
    {
        if (argument < min || argument > max)
        {
            ThrowArgumentOutOfRangeException(paramName, $"Argument not in the range [{min}..{max}]");
        }
        return argument;
    }

    [DoesNotReturn]
    private static void ThrowArgumentNullException(string paramName)
        => throw new ArgumentNullException(paramName);

    [DoesNotReturn]
    private static void ThrowArgumentException(string paramName, string message)
        => throw new ArgumentException(message, paramName);

    [DoesNotReturn]
    private static void ThrowArgumentOutOfRangeException(string paramName, string message)
        => throw new ArgumentOutOfRangeException(paramName, message);
}
