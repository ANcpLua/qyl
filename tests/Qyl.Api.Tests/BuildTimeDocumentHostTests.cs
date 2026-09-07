using Qyl;

namespace Qyl.Api.Tests;

/// <summary>
/// The one place a Qyl API behaves differently under the build-time OpenAPI document tool: it does not go
/// looking for a collector. The recognition is an entry-assembly name, so it is worth pinning.
/// </summary>
public sealed class BuildTimeDocumentHostTests
{
    [Theory]
    [InlineData("GetDocument.Insider", true)]
    [InlineData("getdocument.insider", false)]
    [InlineData("GetDocument.Insider.dll", false)]
    [InlineData("qyl.sample", false)]
    [InlineData(null, false)]
    public void The_document_tool_is_recognised_by_its_entry_assembly(string? entryAssemblyName, bool expected) =>
        Assert.Equal(expected, QylBuildTimeDocumentHost.Matches(entryAssemblyName));

    [Fact]
    public void A_test_host_is_not_the_document_tool() => Assert.False(QylBuildTimeDocumentHost.IsCurrentProcess);
}
