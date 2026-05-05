
namespace Qyl.OpenTelemetry.Extensions;

internal static class Guard
{
    public static void NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
