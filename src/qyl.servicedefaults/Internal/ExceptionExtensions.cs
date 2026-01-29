// =============================================================================
// Exception Extensions - OTel Enterprise Pattern
// Based on: opentelemetry-dotnet-contrib/src/Shared/ExceptionExtensions.cs
// =============================================================================

namespace Qyl.ServiceDefaults.Internal;

/// <summary>
/// Extension methods for exceptions in telemetry contexts.
/// </summary>
internal static class ExceptionExtensions
{
    /// <summary>
    /// Returns a culture-independent string representation of the exception.
    /// </summary>
    /// <remarks>
    /// When recording exceptions in telemetry, culture-invariant output ensures
    /// consistent logs across different server locales.
    /// </remarks>
    public static string ToInvariantString(this Exception exception)
    {
        var originalUICulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            return exception.ToString();
        }
        finally
        {
            Thread.CurrentThread.CurrentUICulture = originalUICulture;
        }
    }
}
