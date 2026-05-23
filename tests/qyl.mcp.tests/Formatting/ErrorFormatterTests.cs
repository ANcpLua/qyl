using qyl.mcp;
using qyl.mcp.Formatting;

namespace Qyl.Mcp.Tests.Formatting;

public sealed class ErrorFormatterTests
{
    [Fact]
    public async Task FormatForLlm_TreatsCancelledTaskAsCancellationNotCollectorTimeout()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var error = new TaskCanceledException(
            "The operation was canceled.",
            innerException: null,
            token: cts.Token);

        var output = ErrorFormatter.FormatForLlm(error, McpTransportMode.Stdio);

        output.Should().Be("**Cancelled:** The operation was cancelled.");
    }
}
