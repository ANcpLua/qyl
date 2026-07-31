using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qyl.Api.Contracts.Workflow;
using Qyl.Cli.Codex;

namespace Qyl.Cli.Tests;

public sealed class WorkflowObserverTests
{
    private static readonly DateTimeOffset s_receivedAt =
        new(2026, 7, 28, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Recorded_app_server_fixture_preserves_attempts_collaboration_and_content()
    {
        var normalizer = new CodexEventNormalizer();
        var events = new List<WorkflowEventAppend>();
        var content = new List<WorkflowContentChunk>();
        events.AddRange(normalizer.StartRun(s_receivedAt).Events);

        var fixture = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "codex-app-server",
            "fanout-interrupt-resume.jsonl");
        foreach (var line in File.ReadLines(fixture))
        {
            using var document = JsonDocument.Parse(line);
            var batch = normalizer.Normalize(document.RootElement, s_receivedAt);
            events.AddRange(batch.Events ?? []);
            content.AddRange(batch.Content ?? []);
        }
        events.AddRange(normalizer.CompleteRun(s_receivedAt.AddMinutes(1), succeeded: true).Events);

        Assert.Equal(
            Enumerable.Range(1, events.Count).Select(static value => (ulong)value),
            events.Select(static workflowEvent => workflowEvent.SourceSequence));
        Assert.Contains(events, static workflowEvent =>
            workflowEvent.Kind is WorkflowJournalEventKind.AgentSpawned &&
            workflowEvent.AgentId?.Value == "thread-worker");
        Assert.Contains(events, static workflowEvent =>
            workflowEvent.Kind is WorkflowJournalEventKind.Joined &&
            workflowEvent.ReceiverAgentId?.Value == "thread-worker");
        Assert.Contains(events, static workflowEvent =>
            workflowEvent.Kind is WorkflowJournalEventKind.FileWritten &&
            workflowEvent.Data!["path"].ToString() == "src/storage.cs");

        var attempts = events
            .Where(static workflowEvent =>
                workflowEvent.Kind is WorkflowJournalEventKind.AttemptCompleted)
            .ToArray();
        Assert.Equal(
            ["attempt-1", "attempt-2"],
            attempts.Select(static item => item.AttemptId?.Value));
        Assert.Equal("interrupted", attempts[0].Data!["status"]);
        Assert.Equal("succeeded", attempts[1].Data!["status"]);
        Assert.Equal("completed", events[^1].Data!["status"]);

        Assert.All(content, static chunk =>
        {
            Assert.StartsWith("sha256:", chunk.ContentRef.Value, StringComparison.Ordinal);
            Assert.Equal(WorkflowContentEncoding.Utf8, chunk.Encoding);
        });
        Assert.Contains(content, static chunk =>
            chunk.Content.Contains("inspect the storage subsystem", StringComparison.Ordinal));
        Assert.Contains(content, static chunk =>
            chunk.Content.Contains("patched storage", StringComparison.Ordinal));
    }

    [Fact]
    public void Normalization_is_idempotent_for_replayed_notifications()
    {
        var normalizer = new CodexEventNormalizer();
        using var document = JsonDocument.Parse(
            """{"method":"thread/started","params":{"thread":{"id":"thread-root","createdAt":1785196800}}}""");

        var first = normalizer.Normalize(document.RootElement, s_receivedAt);
        var replay = normalizer.Normalize(document.RootElement, s_receivedAt);

        Assert.Single(first.Events);
        Assert.Empty(replay.Events);
    }

    [Fact]
    public async Task Spool_encrypts_full_content_and_recovers_acknowledged_progress()
    {
        var root = TemporaryDirectory();
        try
        {
            var store = new WorkflowSpoolStore(root);
            var spool = store.Open("run-encrypted");
            await spool.WriteMetadataAsync(
                Metadata("run-encrypted", "thread-root"),
                TestContext.Current.CancellationToken);
            var entry = Entry(1, "prompt text that must not exist on disk");
            await spool.AppendAsync(entry, TestContext.Current.CancellationToken);

            var raw = await File.ReadAllTextAsync(
                Path.Combine(spool.DirectoryPath, "events.qyl"),
                TestContext.Current.CancellationToken);
            Assert.DoesNotContain("prompt text that must not exist on disk", raw, StringComparison.Ordinal);
            Assert.Equal(entry.Event.EventId, Assert.Single(spool.ReadAfter(0, 10)).Event.EventId);

            await spool.AcknowledgeAsync(1, TestContext.Current.CancellationToken);
            Assert.Empty(spool.ReadAfter(spool.ReadAcknowledgedSourceSequence(), 10));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_retries_from_the_collector_acknowledgement()
    {
        var root = TemporaryDirectory();
        try
        {
            var store = new WorkflowSpoolStore(root);
            var spool = store.Open("run-upload");
            await spool.WriteMetadataAsync(
                Metadata("run-upload", "thread-root"),
                TestContext.Current.CancellationToken);
            await spool.AppendAsync(
                Entry(1, "captured result"),
                TestContext.Current.CancellationToken);

            using var handler = new WorkflowApiHandler();
            using var http = new HttpClient(handler, disposeHandler: false);
            var collector = new WorkflowCollectorClient(
                http,
                new Uri("http://collector.test/api/v1/"),
                "api-key");
            var pump = new WorkflowJournalPump(store, collector);

            Assert.True(await pump.UploadOnceAsync(spool, TestContext.Current.CancellationToken));
            Assert.False(await pump.UploadOnceAsync(spool, TestContext.Current.CancellationToken));
            Assert.Equal((ulong)1, spool.ReadAcknowledgedSourceSequence());
            Assert.Equal(1, handler.AppendCalls);
            Assert.All(handler.ApiKeys, static key => Assert.Equal("api-key", key));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(WorkflowControlAction.Steer, "steer this turn")]
    [InlineData(WorkflowControlAction.Interrupt, null)]
    [InlineData(WorkflowControlAction.Resume, "resume with this input")]
    public async Task Controls_map_to_the_supported_app_server_operations(
        WorkflowControlAction action,
        string? input)
    {
        var client = new RecordingControlClient();
        var command = Command(action, input);
        var turnId = action is WorkflowControlAction.Resume ? null : "turn-active";

        await WorkflowJournalPump.ApplyControlAsync(
            command,
            new CodexControlTarget("thread-root", turnId),
            client,
            TestContext.Current.CancellationToken);

        Assert.Equal(action, client.Action);
        Assert.Equal(
            action is WorkflowControlAction.Interrupt ? null : command.CommandId.Value,
            client.CommandId);
        Assert.Equal(input, client.Input);
    }

    [Fact]
    public async Task Modern_local_bridge_returns_only_the_current_live_run()
    {
        var root = TemporaryDirectory();
        try
        {
            var store = new ActiveWorkflowRunStore(root);
            await store.WriteAsync(
                new ActiveWorkflowRun(
                    "run-live",
                    "thread-live",
                    s_receivedAt,
                    Environment.ProcessId),
                TestContext.Current.CancellationToken);
            using var input = new StringReader(
                """
                {"jsonrpc":"2.0","id":1,"method":"server/discover","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientInfo":{"name":"test","version":"1"},"io.modelcontextprotocol/clientCapabilities":{}}}}
                {"jsonrpc":"2.0","id":2,"method":"tools/list","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientCapabilities":{}}}}
                {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_active_workflow_run","arguments":{},"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientCapabilities":{}}}}
                """);
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            Assert.Equal(
                0,
                await ObserverBridgeServer.RunAsync(
                    store,
                    input,
                    output,
                    TestContext.Current.CancellationToken));

            var responses = output.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, responses.Length);
            using var discovery = JsonDocument.Parse(responses[0]);
            var discoveryResult = discovery.RootElement.GetProperty("result");
            Assert.Equal(
                "2026-07-28",
                discoveryResult.GetProperty("supportedVersions")[0].GetString());
            Assert.Equal("complete", discoveryResult.GetProperty("resultType").GetString());
            Assert.Equal(
                "qyl-observer-bridge",
                discoveryResult
                    .GetProperty("_meta")
                    .GetProperty("io.modelcontextprotocol/serverInfo")
                    .GetProperty("name")
                    .GetString());
            using var response = JsonDocument.Parse(responses[2]);
            var callResult = response.RootElement.GetProperty("result");
            Assert.Equal("complete", callResult.GetProperty("resultType").GetString());
            var structured = callResult.GetProperty("structuredContent");
            Assert.True(structured.GetProperty("active").GetBoolean());
            Assert.True(structured.GetProperty("liveControlsAvailable").GetBoolean());
            Assert.Equal("run-live", structured.GetProperty("runId").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Local_bridge_rejects_legacy_and_incomplete_openings()
    {
        var root = TemporaryDirectory();
        try
        {
            var store = new ActiveWorkflowRunStore(root);
            using var input = new StringReader(
                """
                {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"test","version":"1"}}}
                {"jsonrpc":"2.0","id":2,"method":"server/discover","params":{}}
                {"jsonrpc":"2.0","id":3,"method":"server/discover","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28"}}}
                """);
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            Assert.Equal(
                0,
                await ObserverBridgeServer.RunAsync(
                    store,
                    input,
                    output,
                    TestContext.Current.CancellationToken));

            var responses = output.ToString()
                .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, responses.Length);
            using var legacy = JsonDocument.Parse(responses[0]);
            Assert.Equal(-32022, legacy.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            using var missingVersion = JsonDocument.Parse(responses[1]);
            Assert.Equal(
                -32022,
                missingVersion.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            using var missingCapabilities = JsonDocument.Parse(responses[2]);
            Assert.Equal(
                -32602,
                missingCapabilities.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Schema_verification_rejects_a_control_shape_drift()
    {
        var root = TemporaryDirectory();
        try
        {
            WriteSchemaFixture(root, omitSteerTurn: true);

            var error = Assert.Throws<InvalidDataException>(
                () => CodexSchemaVerifier.VerifyDirectory(root));

            Assert.Contains("expectedTurnId", error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkflowSpoolMetadata Metadata(string runId, string? threadId) =>
        new(
            runId,
            threadId,
            "Fixture run",
            s_receivedAt,
            "codex-cli 0.145.0",
            "sha256:fixture",
            "/fixture",
            false);

    private static WorkflowSpoolEntry Entry(ulong sequence, string content)
    {
        var chunk = new WorkflowContentChunk
        {
            ContentRef = new WorkflowContentRef($"sha256:{new string('a', 64)}"),
            ContentType = "application/json",
            Encoding = WorkflowContentEncoding.Utf8,
            Content = content
        };
        return new WorkflowSpoolEntry(
            new WorkflowEventAppend
            {
                EventId = new WorkflowEventId(
                    $"event-{sequence.ToString(CultureInfo.InvariantCulture)}"),
                SourceSequence = sequence,
                Timestamp = s_receivedAt,
                Kind = WorkflowJournalEventKind.RunCreated,
                ContentRefs = [chunk.ContentRef]
            },
            [chunk]);
    }

    private static WorkflowControlCommand Command(
        WorkflowControlAction action,
        string? input) =>
        new()
        {
            CommandId = new WorkflowCommandId($"command-{action}"),
            RunId = new WorkflowRunId("run-live"),
            Action = action,
            Status = WorkflowControlStatus.Requested,
            IdempotencyKey = $"idempotency-{action}",
            Input = input,
            RequestedAt = s_receivedAt,
            UpdatedAt = s_receivedAt,
            CommandSequence = 1
        };

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteSchemaFixture(string root, bool omitSteerTurn)
    {
        var required = new[]
        {
            "initialize",
            "thread/resume",
            "turn/start",
            "turn/steer",
            "turn/interrupt",
            "thread/started",
            "thread/status/changed",
            "turn/started",
            "turn/completed",
            "item/started",
            "item/completed",
            "serverRequest/resolved"
        };
        File.WriteAllText(
            Path.Combine(root, "codex_app_server_protocol.schemas.json"),
            JsonSerializer.Serialize(required, WorkflowObserverTestJsonContext.Default.StringArray));
        File.WriteAllText(
            Path.Combine(root, "codex_app_server_protocol.v2.schemas.json"),
            """
            ["collabAgentToolCall","senderThreadId","receiverThreadIds","commandExecution","fileChange","mcpToolCall"]
            """);
        var v2 = Path.Combine(root, "v2");
        Directory.CreateDirectory(v2);
        WriteProperties(
            Path.Combine(v2, "TurnSteerParams.json"),
            omitSteerTurn ? ["threadId", "input"] : ["threadId", "expectedTurnId", "input"]);
        WriteProperties(Path.Combine(v2, "TurnInterruptParams.json"), ["threadId", "turnId"]);
        WriteProperties(Path.Combine(v2, "TurnStartParams.json"), ["threadId", "input"]);
        WriteProperties(
            Path.Combine(v2, "ItemStartedNotification.json"),
            ["threadId", "turnId", "item", "startedAtMs"]);
        WriteProperties(
            Path.Combine(v2, "ItemCompletedNotification.json"),
            ["threadId", "turnId", "item", "completedAtMs"]);
    }

    private static void WriteProperties(string path, IReadOnlyList<string> properties)
    {
        var declarations = string.Join(
            ',',
            properties.Select(static property => $"\"{property}\":{{\"type\":\"string\"}}"));
        File.WriteAllText(path, $"{{\"type\":\"object\",\"properties\":{{{declarations}}}}}");
    }

    private sealed class WorkflowApiHandler : HttpMessageHandler
    {
        public int AppendCalls { get; private set; }
        public List<string?> ApiKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ApiKeys.Add(request.Headers.TryGetValues("x-otlp-api-key", out var values)
                ? values.Single()
                : null);
            if (request.RequestUri!.AbsolutePath.EndsWith("/events", StringComparison.Ordinal))
            {
                AppendCalls++;
                return Task.FromResult(JsonResponse(
                    """
                    {"accepted_count":1,"duplicate_count":0,"acknowledged_source_sequence":"1","first_journal_sequence":"1","last_journal_sequence":"1"}
                    """));
            }
            return Task.FromResult(JsonResponse("{}"));
        }

        private static HttpResponseMessage JsonResponse(string json) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }

    private sealed class RecordingControlClient : ICodexControlClient
    {
        public WorkflowControlAction? Action { get; private set; }
        public string? CommandId { get; private set; }
        public string? Input { get; private set; }

        public Task<JsonElement> SteerAsync(
            string threadId,
            string turnId,
            string commandId,
            string input,
            CancellationToken cancellationToken)
        {
            Action = WorkflowControlAction.Steer;
            CommandId = commandId;
            Input = input;
            return Task.FromResult(default(JsonElement));
        }

        public Task<JsonElement> InterruptAsync(
            string threadId,
            string turnId,
            CancellationToken cancellationToken)
        {
            Action = WorkflowControlAction.Interrupt;
            return Task.FromResult(default(JsonElement));
        }

        public Task<JsonElement> ResumeAsync(
            string threadId,
            string commandId,
            string input,
            CancellationToken cancellationToken)
        {
            Action = WorkflowControlAction.Resume;
            CommandId = commandId;
            Input = input;
            return Task.FromResult(default(JsonElement));
        }
    }
}

[JsonSerializable(typeof(string[]))]
internal sealed partial class WorkflowObserverTestJsonContext : JsonSerializerContext;
