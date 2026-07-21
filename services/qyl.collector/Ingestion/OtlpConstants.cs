namespace Qyl.Collector.Ingestion;

internal static class OtlpConstants
{
    public static readonly string[] Paths = ["/v1/traces", "/v1/logs", "/v1/metrics"];

    private static readonly string[] s_namespacePaths = ["/v1", "/v1development"];

    public static bool IsOtlpPath(string path) =>
        Paths.Any(candidate => IsExactPathOrTrailingSlash(path, candidate));

    // The dashboard must never claim an OTLP namespace, including unknown routes that should
    // remain protocol 404s instead of becoming an HTML SPA fallback.
    public static bool IsOtlpNamespacePath(string path) =>
        s_namespacePaths.Any(candidate =>
            path.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(candidate + "/", StringComparison.OrdinalIgnoreCase));

    private static bool IsExactPathOrTrailingSlash(string path, string candidate) =>
        path.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
        (path.Length == candidate.Length + 1 &&
         path[^1] is '/' &&
         path.AsSpan(0, candidate.Length).Equals(candidate, StringComparison.OrdinalIgnoreCase));
}
