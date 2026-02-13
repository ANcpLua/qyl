namespace qyl.collector.Errors;

public static class ErrorCategorizer
{
    public static string Categorize(string exceptionType, string? genAiErrorType = null)
    {
        // GenAI-specific error type takes precedence
        if (!string.IsNullOrEmpty(genAiErrorType))
        {
            return genAiErrorType switch
            {
                "rate_limit_exceeded" or "insufficient_quota" => "rate_limit",
                "context_length_exceeded" => "validation",
                "authentication_error" => "auth",
                "model_overloaded" => "external",
                "timeout" => "timeout",
                "content_filter" => "validation",
                _ => "unknown"
            };
        }

        // .NET exception type mapping
        return exceptionType switch
        {
            _ when exceptionType.Contains("HttpRequestException", StringComparison.Ordinal) => "network",
            _ when exceptionType.Contains("SocketException", StringComparison.Ordinal) => "network",
            _ when exceptionType.Contains("TimeoutException", StringComparison.Ordinal) => "timeout",
            _ when exceptionType.Contains("TaskCanceledException", StringComparison.Ordinal) => "timeout",
            _ when exceptionType.Contains("UnauthorizedAccess", StringComparison.Ordinal) => "auth",
            _ when exceptionType.Contains("Authentication", StringComparison.Ordinal) => "auth",
            _ when exceptionType.Contains("DbException", StringComparison.Ordinal) => "database",
            _ when exceptionType.Contains("DuckDB", StringComparison.Ordinal) => "database",
            _ when exceptionType.Contains("SqlException", StringComparison.Ordinal) => "database",
            _ when exceptionType.Contains("ArgumentException", StringComparison.Ordinal) => "validation",
            _ when exceptionType.Contains("ArgumentNull", StringComparison.Ordinal) => "validation",
            _ when exceptionType.Contains("FormatException", StringComparison.Ordinal) => "validation",
            _ when exceptionType.Contains("InvalidOperation", StringComparison.Ordinal) => "internal",
            _ when exceptionType.Contains("NotSupported", StringComparison.Ordinal) => "internal",
            _ when exceptionType.Contains("NotImplemented", StringComparison.Ordinal) => "internal",
            _ when exceptionType.Contains("NullReference", StringComparison.Ordinal) => "internal",
            _ => "unknown"
        };
    }
}
