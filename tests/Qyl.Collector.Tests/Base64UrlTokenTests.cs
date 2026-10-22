using System.Text.Json.Nodes;
using Qyl.Collector.Analysis;
using Qyl.Collector.Hosting;

namespace Qyl.Collector.Tests;

/// <summary>
/// Cursors and analysis handles travel as unpadded base64url. A token spelled in the standard base64 alphabet is
/// none this Collector issued.
/// </summary>
[TestClass]
public sealed class Base64UrlTokenTests
{
    private const string Project = "alpha";
    private const string Kind = "cursor";

    [TestMethod]
    public void PageCursor_StandardBase64Alphabet_IsRejected()
    {
        var standard = PageCursorCodec.Encode(PageCursorKind.SavedQueries, "~~~").Replace('-', '+').Replace('_', '/');

        Assert.IsFalse(PageCursorCodec.TryDecode(standard, PageCursorKind.SavedQueries, 1, out _));
    }

    [TestMethod]
    public void PageCursor_IssuedCursor_RoundTripsItsFields()
    {
        var cursor = PageCursorCodec.Encode(PageCursorKind.SavedQueries, "~~~");

        Assert.IsTrue(PageCursorCodec.TryDecode(cursor, PageCursorKind.SavedQueries, 1, out var fields));
        Assert.AreSequenceEqual(["~~~"], fields);
    }

    [TestMethod]
    public void AnalysisHandle_StandardBase64Alphabet_IsRejected()
    {
        var handles = new AnalysisHandles(new byte[32]);
        var payload = handles.Issue(Project, Kind, new JsonObject { ["position"] = "~~~" }).Split('.');
        var standard = payload[0].Replace('-', '+').Replace('_', '/') + "." + payload[1];

        var error = Assert.ThrowsExactly<AnalysisInputException>(() => handles.Read(standard, Project, Kind));

        Assert.AreEqual("cursor.invalid", error.Code);
    }

    [TestMethod]
    public void AnalysisHandle_IssuedHandle_ReadsItsValue()
    {
        var handles = new AnalysisHandles(new byte[32]);
        var handle = handles.Issue(Project, Kind, new JsonObject { ["position"] = "~~~" });

        Assert.AreEqual("~~~", handles.Read(handle, Project, Kind)["position"]!.GetValue<string>());
    }
}
