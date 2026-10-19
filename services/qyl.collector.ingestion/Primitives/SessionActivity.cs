namespace Qyl.Collector.Ingestion.Primitives;

internal static class SessionActivity
{
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(30);

    // Activity at the cutoff is still active. Keep this cutoff exact through SQL and mapping.
    public static ulong GetActiveCutoffUnixNano(DateTimeOffset now) =>
        TimeConversions.ToUnixNanoUnsigned(now - ActiveWindow);
}
