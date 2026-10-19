namespace Qyl.Api.Tests;

/// <summary>
/// The one place a Qyl API behaves differently under the build-time OpenAPI document tool: it does not go
/// looking for a collector. The recognition is an entry-assembly name, so it is worth pinning.
/// </summary>
[TestClass]
public sealed class BuildTimeDocumentHostTests
{
    [TestMethod]
    [DataRow("GetDocument.Insider", true)]
    [DataRow("getdocument.insider", false)]
    [DataRow("GetDocument.Insider.dll", false)]
    [DataRow("qyl.sample", false)]
    [DataRow(null, false)]
    public void The_document_tool_is_recognised_by_its_entry_assembly(string? entryAssemblyName, bool expected) =>
        Assert.AreEqual(expected, QylBuildTimeDocumentHost.Matches(entryAssemblyName));

    [TestMethod]
    public void A_test_host_is_not_the_document_tool() => Assert.IsFalse(QylBuildTimeDocumentHost.IsCurrentProcess);
}
