namespace Qyl.Collector.Ingestion;

public static class OtlpConstants
{
    public static readonly string[] Paths = ["/v1/traces", "/v1/logs", "/v1/profiles"];

    public static bool IsOtlpPath(string path) =>
        Paths.Any(path.StartsWithIgnoreCase);
}
