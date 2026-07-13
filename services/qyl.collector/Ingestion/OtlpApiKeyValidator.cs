namespace Qyl.Collector.Ingestion;

/// <summary>
/// The one key check both transports share: the HTTP middleware and the gRPC interceptor
/// validate the same primary/secondary pair with the same fixed-time comparison.
/// </summary>
internal static class OtlpApiKeyValidator
{
    public static bool IsValid(string? candidate, OtlpApiKeyOptions options)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        return FixedTimeEquals(candidate, options.PrimaryApiKey) ||
               FixedTimeEquals(candidate, options.SecondaryApiKey);
    }

    private static bool FixedTimeEquals(string candidate, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || candidate.Length != expected.Length)
            return false;

        var diff = 0;
        for (var i = 0; i < candidate.Length; i++)
            diff |= candidate[i] ^ expected[i];

        return diff is 0;
    }
}
