namespace qyl.collector.Errors;

public static class ErrorCategorizer
{
    public static string Categorize(
        string exceptionType,
        string? genAiErrorType = null,
        string? finishReason = null,
        string? message = null)
    {
        // GenAI-specific error type takes precedence
        if (!string.IsNullOrEmpty(genAiErrorType))
        {
            return genAiErrorType switch
            {
                "rate_limit_exceeded" or "insufficient_quota" => "rate_limit",
                "context_length_exceeded" or "max_tokens_exceeded" => "token_limit",
                "authentication_error" => "auth",
                "model_overloaded" => "external",
                "timeout" => "timeout",
                "content_filter" or "content_policy_violation" => "content_filter",
                "hallucination_detected" or "model_not_found" or "model_not_available" => "model_error",
                "tool_execution_error" or "tool_not_found" or "tool_call_failed" => "tool_execution_error",
                _ => "unknown"
            };
        }

        // Infer GenAI category from finish reasons
        if (!string.IsNullOrEmpty(finishReason))
        {
            if (finishReason.ContainsOrdinal("content_filter"))
                return "content_filter";
            if (finishReason.ContainsOrdinal("length"))
                return "token_limit";
        }

        // Infer GenAI category from error message patterns
        if (!string.IsNullOrEmpty(message))
        {
            if (message.ContainsOrdinal("rate limit") || message.ContainsOrdinal("429") || message.ContainsOrdinal("Too Many Requests"))
                return "rate_limit";
            if (message.ContainsOrdinal("content filter") || message.ContainsOrdinal("content management policy") || message.ContainsOrdinal("content_policy"))
                return "content_filter";
            if (message.ContainsOrdinal("maximum context length") || message.ContainsOrdinal("token limit") || message.ContainsOrdinal("max_tokens"))
                return "token_limit";
            if (message.ContainsOrdinal("tool") && (message.ContainsOrdinal("failed") || message.ContainsOrdinal("error")))
                return "tool_execution_error";
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
