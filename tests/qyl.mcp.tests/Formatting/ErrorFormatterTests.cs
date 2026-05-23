using System.Net;
using qyl.mcp;
using qyl.mcp.Formatting;

namespace Qyl.Mcp.Tests.Formatting;

public sealed class ErrorFormatterTests
{
    [Theory]
    [InlineData(HttpStatusCode.NotFound, "**Not Found**")]
    [InlineData(HttpStatusCode.BadRequest, "**Invalid Request**")]
    [InlineData(HttpStatusCode.Unauthorized, "**Authentication Required**")]
    [InlineData(HttpStatusCode.Forbidden, "**Access Denied**")]
    [InlineData(HttpStatusCode.InternalServerError, "**Collector Error**")]
    [InlineData(HttpStatusCode.BadGateway, "**Collector Error**")]
    [InlineData(HttpStatusCode.RequestTimeout, "**Connection Error**")]
    public void FormatForLlm_CategorisesHttpErrorsByStatus(HttpStatusCode status, string category) =>
        ErrorFormatter.FormatForLlm(new HttpRequestException("boom", inner: null, status), McpTransportMode.Http)
            .Should().StartWith(category);

    [Fact]
    public void FormatForLlm_TreatsCancelledTaskAsCancellationNotTimeout()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        ErrorFormatter.FormatForLlm(new TaskCanceledException("op cancelled", innerException: null, cts.Token), McpTransportMode.Stdio)
            .Should().Be("**Cancelled:** The operation was cancelled.");
    }

    [Fact]
    public void FormatForLlm_TreatsTaskCanceledWithoutTokenAsTimeout() =>
        ErrorFormatter.FormatForLlm(new TaskCanceledException("timeout"), McpTransportMode.Stdio)
            .Should().StartWith("**Timeout:**");

    [Fact]
    public void FormatForLlm_FormatsOperationCancelledWithBudgetHint_WhenMessageMentionsToolCallLimit() =>
        ErrorFormatter.FormatForLlm(new OperationCanceledException("tool call limit exceeded"), McpTransportMode.Stdio)
            .Should().StartWith("**Investigation Budget Reached**");

    [Theory]
    [InlineData(true, "MCP server process is running")]
    [InlineData(false, "endpoint URL and network reachability")]
    public void FormatForLlm_AdaptsTransportHintForIoException(bool stdio, string hintFragment) =>
        ErrorFormatter.FormatForLlm(new IOException("pipe closed"), Transport(stdio))
            .Should().Contain(hintFragment);

    [Theory]
    [InlineData(true, "Check your environment variables")]
    [InlineData(false, "Contact the administrator")]
    public void FormatForLlm_AdaptsTransportHintForConfigError(bool stdio, string hintFragment) =>
        ErrorFormatter.FormatForLlm(new InvalidOperationException("misconfigured"), Transport(stdio))
            .Should().Contain(hintFragment);

    [Theory]
    [InlineData(true, "kaboom")]
    [InlineData(false, "An unexpected error occurred")]
    public void FormatForLlm_LeaksMessageOnlyOverStdio_ForUnknownExceptions(bool stdio, string fragment) =>
        ErrorFormatter.FormatForLlm(new ArgumentException("kaboom"), Transport(stdio))
            .Should().Contain(fragment);

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true, "QYL_MCP_TOKEN")]
    [InlineData(HttpStatusCode.Unauthorized, false, "Re-authenticate")]
    public void FormatForLlm_AdaptsAuthHintByTransport(HttpStatusCode status, bool stdio, string hintFragment) =>
        ErrorFormatter.FormatForLlm(new HttpRequestException("no auth", inner: null, status), Transport(stdio))
            .Should().Contain(hintFragment);

    private static McpTransportMode Transport(bool stdio) =>
        stdio ? McpTransportMode.Stdio : McpTransportMode.Http;
}
