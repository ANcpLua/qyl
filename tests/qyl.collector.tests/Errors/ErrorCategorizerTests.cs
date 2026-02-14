using qyl.collector.Errors;

namespace qyl.collector.tests.Errors;

public sealed class ErrorCategorizerTests
{
    [Theory]
    [InlineData("System.Net.Http.HttpRequestException", null, "network")]
    [InlineData("System.TimeoutException", null, "timeout")]
    [InlineData("System.UnauthorizedAccessException", null, "auth")]
    [InlineData("System.Data.Common.DbException", null, "database")]
    [InlineData("DuckDB.NET.DuckDBException", null, "database")]
    [InlineData("System.ArgumentException", null, "validation")]
    [InlineData("System.ArgumentNullException", null, "validation")]
    [InlineData("SomeCustomException", null, "unknown")]
    public void Categorize_ByExceptionType_ReturnsExpected(string exceptionType, string? genAiErrorType, string expected)
    {
        var result = ErrorCategorizer.Categorize(exceptionType, genAiErrorType);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("rate_limit_exceeded", "rate_limit")]
    [InlineData("context_length_exceeded", "token_limit")]
    [InlineData("authentication_error", "auth")]
    [InlineData("insufficient_quota", "rate_limit")]
    [InlineData("model_overloaded", "external")]
    [InlineData("timeout", "timeout")]
    public void Categorize_ByGenAiErrorType_TakesPrecedence(string genAiErrorType, string expected)
    {
        var result = ErrorCategorizer.Categorize("System.Exception", genAiErrorType);
        Assert.Equal(expected, result);
    }
}
