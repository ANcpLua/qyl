using System.Text.RegularExpressions;

namespace Qyl.Collector.Errors;

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
        string? category = null,
        string? serviceName = null,
        string? spanName = null)
    {
        var normalizedStack = NormalizeStackTrace(stackTrace);
        var normalizedMessage = NormalizeMessage(message);

        // v2: service.name scopes fingerprints per service (prevents cross-service merging)
        var input = !string.IsNullOrEmpty(serviceName)
            ? $"{serviceName}\n{exceptionType}\n{normalizedMessage}\n{normalizedStack}"
            : $"{exceptionType}\n{normalizedMessage}\n{normalizedStack}";

        if (!string.IsNullOrEmpty(spanName))
            input = $"{input}\n{spanName}";
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
                    if (!string.IsNullOrEmpty(serviceName))
                        input = $"{serviceName}\n{input}";
                    break;
                case "content_filter" when !string.IsNullOrEmpty(genAiModel):
                    // Group content filter errors by model
                    input = $"content_filter\n{genAiModel}";
                    if (!string.IsNullOrEmpty(serviceName))
                        input = $"{serviceName}\n{input}";
                    break;
                case "token_limit" when !string.IsNullOrEmpty(genAiModel):
                    // Group token limit errors by model
                    input = $"token_limit\n{genAiModel}";
                    if (!string.IsNullOrEmpty(serviceName))
                        input = $"{serviceName}\n{input}";
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

        return ANcpLua.Roslyn.Utilities.Security.Sha256Hex.Hash(input)[..16]; // 64-bit fingerprint
    }

    private static string NormalizeStackTrace(string? stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace)) return "";
        var result = LineNumberRegex().Replace(stackTrace, "");
        result = FilePathRegex().Replace(result, "");
        result = CollapseFrameworkFrames(result);
        return result.Trim();
    }

    /// <summary>
    ///     Collapses consecutive .NET framework/pipeline frames into a single [framework] marker.
    ///     Prevents ASP.NET Core middleware pipeline depth from splitting otherwise-identical issues.
    /// </summary>
    private static string CollapseFrameworkFrames(string stackTrace)
    {
        var lines = stackTrace.Split('\n');
        var sb = new StringBuilder();
        var inFramework = false;
        foreach (var line in lines)
        {
            if (IsFrameworkFrame(line))
            {
                if (!inFramework)
                {
                    sb.AppendLine("[framework]");
                    inFramework = true;
                }
            }
            else
            {
                inFramework = false;
                sb.AppendLine(line);
            }
        }

        return sb.ToString();
    }

    private static bool IsFrameworkFrame(ReadOnlySpan<char> line) =>
        line.Contains("Microsoft.AspNetCore.", StringComparison.Ordinal) ||
        line.Contains("Microsoft.Extensions.", StringComparison.Ordinal) ||
        line.Contains("System.Runtime.", StringComparison.Ordinal) ||
        line.Contains("System.Threading.", StringComparison.Ordinal) ||
        line.Contains("System.Private.CoreLib", StringComparison.Ordinal);

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
