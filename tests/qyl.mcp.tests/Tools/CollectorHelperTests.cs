using qyl.mcp.Tools;

namespace Qyl.Mcp.Tests.Tools;

public sealed class CollectorHelperTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsOperationResult_WhenNoExceptionThrown() =>
        (await CollectorHelper.ExecuteAsync(static () => Task.FromResult("ok")))
            .Should().Be("ok");

    [Fact]
    public async Task ExecuteAsync_FormatsDirectOperationCancellation() =>
        (await CollectorHelper.ExecuteAsync(static () => throw new OperationCanceledException()))
            .Should().Be("**Cancelled:** The operation was cancelled.");

    [Fact]
    public async Task ExecuteAsync_FormatsTaskCanceledFromTimeout() =>
        (await CollectorHelper.ExecuteAsync(static () => throw new TaskCanceledException("hit timeout")))
            .Should().StartWith("**Timeout:**");

    [Theory]
    [InlineData(null, "**Cancelled:**")]
    [InlineData("InvestigationBudget", "InvestigationBudget: **Cancelled:**")]
    public async Task ExecuteAsync_PrefixesFormattedError_WhenPrefixSupplied(string? prefix, string expectedStart) =>
        (await CollectorHelper.ExecuteAsync(
            static () => throw new OperationCanceledException(),
            prefix))
            .Should().StartWith(expectedStart);
}
