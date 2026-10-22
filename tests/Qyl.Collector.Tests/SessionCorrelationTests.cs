using Microsoft.Extensions.Time.Testing;
using OpenTelemetry.Proto.Common.V1;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class SessionCorrelationTests
{
    private const string CodexExecScope = "codex_exec";
    private const string CodexService = "codex_cli_rs";
    private const string DotNetScope = "System.Threading";
    private const string CodexThreadUuid = "0199a213-81c0-7800-8aa1-bbab2a035a53";
    private const string OtherUuid = "0199a213-81c0-7800-8aa1-000000000002";
    private const string SessionIdKey = "session.id";
    private const string McpSessionIdKey = "mcp.session.id";
    private const string GenAiConversationIdKey = "gen_ai.conversation.id";
    private const string ConversationIdKey = "conversation.id";
    private const string ThreadIdKey = "thread.id";

    [TestMethod]
    public void SpanSessionId_ConversationIdOnly_IsTheConversationId()
    {
        var row = SpanRow(DotNetScope, [OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid)]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    [DataRow(SessionIdKey)]
    [DataRow(McpSessionIdKey)]
    [DataRow(GenAiConversationIdKey)]
    public void SpanSessionId_ExistingKeyBesideConversationId_PrefersExistingKey(string precedingKey)
    {
        var row = SpanRow(DotNetScope,
        [
            OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid),
            OtlpTestSpans.String(precedingKey, OtherUuid)
        ]);

        Assert.AreEqual(OtherUuid, row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_ConversationIdAndCodexThreadId_PrefersConversationId()
    {
        var row = SpanRow(CodexExecScope,
        [
            OtlpTestSpans.String(ThreadIdKey, OtherUuid),
            OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid)
        ]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_UuidThreadIdOnCodexScope_IsTheThreadId()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.String(ThreadIdKey, CodexThreadUuid)]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_UuidThreadIdOnCodexService_IsTheThreadId()
    {
        var row = SpanRow(DotNetScope, [OtlpTestSpans.String(ThreadIdKey, CodexThreadUuid)], CodexService);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_IntegerThreadIdOnDotNetScope_IsNull()
    {
        var row = SpanRow(DotNetScope, [OtlpTestSpans.Int(ThreadIdKey, 12)]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_UuidThreadIdOnDotNetScope_IsNull()
    {
        var row = SpanRow(DotNetScope, [OtlpTestSpans.String(ThreadIdKey, CodexThreadUuid)]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_IntegerThreadIdOnCodexScope_IsNull()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.Int(ThreadIdKey, 12)]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_NonUuidThreadIdOnCodexScope_IsNull()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.String(ThreadIdKey, "12")]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    [DataRow(GenAiConversationIdKey)]
    [DataRow(ConversationIdKey)]
    [DataRow(ThreadIdKey)]
    public void SpanSessionId_SameUuidUnderAnyCodexKey_IsTheSameSession(string carrierKey)
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.String(carrierKey, CodexThreadUuid)]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void SpanAttributes_ConversationIdProjected_KeepsConversationId()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid)]);

        Assert.Contains(
            $"\"{ConversationIdKey}\":\"{CodexThreadUuid}\"",
            row.AttributesJson ?? string.Empty,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpanAttributes_CodexThreadIdProjected_KeepsThreadId()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.String(ThreadIdKey, CodexThreadUuid)]);

        Assert.Contains(
            $"\"{ThreadIdKey}\":\"{CodexThreadUuid}\"",
            row.AttributesJson ?? string.Empty,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void SpanSessionId_CodexScopeWithoutAnyId_IsNull()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.String("http.route", "/v1/responses")]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    [DataRow(SessionIdKey)]
    [DataRow(McpSessionIdKey)]
    [DataRow(GenAiConversationIdKey)]
    [DataRow(ConversationIdKey)]
    public void SpanSessionId_BlankId_IsNull(string sessionKey)
    {
        var row = SpanRow(DotNetScope, [OtlpTestSpans.String(sessionKey, " ")]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_BlankSessionIdBesideConversationId_IsTheConversationId()
    {
        var row = SpanRow(DotNetScope,
        [
            OtlpTestSpans.String(SessionIdKey, string.Empty),
            OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid)
        ]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void SpanSessionId_NonStringConversationId_IsNull()
    {
        var row = SpanRow(CodexExecScope, [OtlpTestSpans.Int(ConversationIdKey, 12)]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    public void LogSessionId_ConversationIdOnCodexLog_IsTheConversationId()
    {
        var row = LogRow(CodexExecScope, [OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid)]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void LogSessionId_UuidThreadIdOnCodexLog_IsTheThreadId()
    {
        var row = LogRow(CodexExecScope, [OtlpTestSpans.String(ThreadIdKey, CodexThreadUuid)]);

        Assert.AreEqual(CodexThreadUuid, row.SessionId);
    }

    [TestMethod]
    public void LogSessionId_IntegerThreadIdOnDotNetLog_IsNull()
    {
        var row = LogRow(DotNetScope, [OtlpTestSpans.Int(ThreadIdKey, 12)]);

        Assert.IsNull(row.SessionId);
    }

    [TestMethod]
    public void LogAttributes_ConversationIdProjected_KeepsConversationId()
    {
        var row = LogRow(CodexExecScope, [OtlpTestSpans.String(ConversationIdKey, CodexThreadUuid)]);

        Assert.Contains(
            $"\"{ConversationIdKey}\":\"{CodexThreadUuid}\"",
            row.AttributesJson ?? string.Empty,
            StringComparison.Ordinal);
    }

    private static SpanStorageRow SpanRow(string scopeName, KeyValue[] attributes, string serviceName = "app-service") =>
        IngestionStorageMapper.ToSpanStorageRows(
            OtlpConverter.ConvertTraceRequest(OtlpTestSpans.Request(scopeName, attributes, serviceName: serviceName)))[0];

    private static LogStorageRow LogRow(string scopeName, KeyValue[] attributes) =>
        IngestionStorageMapper.ToLogStorageRows(
            OtlpConverter.ConvertLogs(
                OtlpTestLogs.Request(scopeName, attributes),
                new FakeTimeProvider(CollectorApiTestHost.FixtureUtcNow)))[0];
}
