namespace Qyl.Collector.Ingestion;

internal static class OtlpConstants
{
    public static readonly string[] Paths = ["/v1/traces", "/v1/logs", "/v1development/profiles"];

    private static readonly string[] s_namespacePaths = Paths
        .Select(static path => path.IndexOf('/', 1) is var separator && separator >= 0
            ? path[..separator]
            : path)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

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
