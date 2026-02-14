using System.Text.RegularExpressions;

namespace qyl.collector.Errors;

public static partial class ErrorFingerprinter
{
    public static string Compute(
        string exceptionType,
        string message,
        string? stackTrace,
        string? genAiOperation = null,
        string? genAiProvider = null,
        string? genAiModel = null,
        string? finishReason = null,
        string? category = null)
    {
        var normalizedStack = NormalizeStackTrace(stackTrace);
        var normalizedMessage = NormalizeMessage(message);

        var input = $"{exceptionType}\n{normalizedMessage}\n{normalizedStack}";
        if (!string.IsNullOrEmpty(genAiOperation))
            input = $"{input}\n{genAiOperation}";

        // GenAI-aware grouping: add dimensions based on error category
        if (!string.IsNullOrEmpty(category))
        {
            switch (category)
            {
                case "rate_limit" when !string.IsNullOrEmpty(genAiProvider):
                    // Group rate limit errors by provider (same provider = same fingerprint)
                    input = $"rate_limit\n{genAiProvider}";
                    break;
                case "content_filter" when !string.IsNullOrEmpty(genAiModel):
                    // Group content filter errors by model
                    input = $"content_filter\n{genAiModel}";
                    break;
                case "token_limit" when !string.IsNullOrEmpty(genAiModel):
                    // Group token limit errors by model
                    input = $"token_limit\n{genAiModel}";
                    break;
                default:
                    // For other GenAI errors, include provider and finish reason as dimensions
                    if (!string.IsNullOrEmpty(genAiProvider))
                        input = $"{input}\n{genAiProvider}";
                    if (!string.IsNullOrEmpty(finishReason))
                        input = $"{input}\n{finishReason}";
                    break;
            }
        }

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
