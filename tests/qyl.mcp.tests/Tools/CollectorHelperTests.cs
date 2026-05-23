using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class CollectorHelperTests
{
    [Fact]
    public async Task ExecuteAsync_FormatsDirectOperationCancellation()
    {
        var output = await CollectorHelper.ExecuteAsync(
            static () => throw new OperationCanceledException());

        output.Should().Be("**Cancelled:** The operation was cancelled.");
    }
}
