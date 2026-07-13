using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Qyl.Host;

internal static class QylGuard
{
    internal static void NotNull(
        [NotNull] object? value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
    }

    internal static void NotNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
    }
}
