namespace Qyl.Collector.Ingestion;

internal static class OtlpConstants
{
    public static readonly string[] Paths = ["/v1/traces", "/v1/logs", "/v1/profiles"];

    public static bool IsOtlpPath(string path) =>
        Paths.Any(candidate => IsExactPathOrTrailingSlash(path, candidate));

    private static bool IsExactPathOrTrailingSlash(string path, string candidate) =>
        path.Equals(candidate, StringComparison.OrdinalIgnoreCase) ||
        (path.Length == candidate.Length + 1 &&
         path.EndsWith('/', StringComparison.Ordinal) &&
         path.AsSpan(0, candidate.Length).Equals(candidate, StringComparison.OrdinalIgnoreCase));
}
