using System.Text.RegularExpressions;

namespace qyl.collector.Errors;

public static partial class ErrorFingerprinter
{
    public static string Compute(
        string exceptionType,
        string message,
        string? stackTrace,
        string? genAiOperation = null)
    {
        var normalizedStack = NormalizeStackTrace(stackTrace);
        var normalizedMessage = NormalizeMessage(message);

        var input = $"{exceptionType}\n{normalizedMessage}\n{normalizedStack}";
        if (!string.IsNullOrEmpty(genAiOperation))
            input = $"{input}\n{genAiOperation}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash)[..16]; // 64-bit fingerprint
    }

    private static string NormalizeStackTrace(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return "";
        var result = LineNumberRegex().Replace(stackTrace, "");
        result = FilePathRegex().Replace(result, "");
        return result.Trim();
    }

    private static string NormalizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message)) return "";
        var result = GuidRegex().Replace(message, "<GUID>");
        result = StandaloneNumberRegex().Replace(result, "<N>");
        result = UrlRegex().Replace(result, "<URL>");
        return result;
    }

    [GeneratedRegex(@" in [^\s]+:\s*line \d+")]
    private static partial Regex LineNumberRegex();

    [GeneratedRegex(@" in [/\\][^\s]+\.(cs|fs|vb)")]
    private static partial Regex FilePathRegex();

    [GeneratedRegex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"(?<![a-zA-Z])\d{5,}(?![a-zA-Z])")]
    private static partial Regex StandaloneNumberRegex();

    [GeneratedRegex(@"https?://[^\s]+")]
    private static partial Regex UrlRegex();
}
