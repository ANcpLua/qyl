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
            _ when exceptionType.ContainsOrdinal("HttpRequestException") => "network",
            _ when exceptionType.ContainsOrdinal("SocketException") => "network",
            _ when exceptionType.ContainsOrdinal("TimeoutException") => "timeout",
            _ when exceptionType.ContainsOrdinal("TaskCanceledException") => "timeout",
            _ when exceptionType.ContainsOrdinal("UnauthorizedAccess") => "auth",
            _ when exceptionType.ContainsOrdinal("Authentication") => "auth",
            _ when exceptionType.ContainsOrdinal("DbException") => "database",
            _ when exceptionType.ContainsOrdinal("DuckDB") => "database",
            _ when exceptionType.ContainsOrdinal("SqlException") => "database",
            _ when exceptionType.ContainsOrdinal("ArgumentException") => "validation",
            _ when exceptionType.ContainsOrdinal("ArgumentNull") => "validation",
            _ when exceptionType.ContainsOrdinal("FormatException") => "validation",
            _ when exceptionType.ContainsOrdinal("InvalidOperation") => "internal",
            _ when exceptionType.ContainsOrdinal("NotSupported") => "internal",
            _ when exceptionType.ContainsOrdinal("NotImplemented") => "internal",
            _ when exceptionType.ContainsOrdinal("NullReference") => "internal",
            _ => "unknown"
        };
    }
}
