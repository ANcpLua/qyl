using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Qyl.Api.Contracts.Workflow;
using Qyl.Cli.Codex;

namespace Qyl.Cli.Tests;

public sealed class DiagnosticSnapshotTests
{
    private static readonly DateTimeOffset s_timestamp =
        new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Bridge_discovers_the_bounded_diagnostic_tool()
    {
        var root = TemporaryDirectory();
        try
        {
            var store = new ActiveWorkflowRunStore(root);
            using var input = new StringReader(
                """
                {"jsonrpc":"2.0","id":1,"method":"tools/list","params":{"_meta":{"io.modelcontextprotocol/protocolVersion":"2026-07-28","io.modelcontextprotocol/clientCapabilities":{}}}}
                """);
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            Assert.Equal(
                0,
                await ObserverBridgeServer.RunAsync(
                    store,
                    input,
                    output,
                    TestContext.Current.CancellationToken));

            using var response = JsonDocument.Parse(output.ToString());
            var tool = response.RootElement
                .GetProperty("result")
                .GetProperty("tools")
                .EnumerateArray()
                .Single(static item => item.GetProperty("name").GetString() == "record_diagnostic_snapshot");
            var schema = tool.GetProperty("inputSchema");
            var description = tool.GetProperty("description").GetString();
            Assert.Contains("Reuse the same snapshotId", description, StringComparison.Ordinal);
            Assert.Contains("sensitive values are redacted", description, StringComparison.Ordinal);
            Assert.Contains("secret values are omitted", description, StringComparison.Ordinal);
            Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(64, schema.GetProperty("properties").GetProperty("variables").GetProperty("maxItems").GetInt32());
            Assert.Equal(64, schema.GetProperty("properties").GetProperty("checks").GetProperty("maxItems").GetInt32());
            Assert.False(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
            Assert.True(tool.GetProperty("annotations").GetProperty("idempotentHint").GetBoolean());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Bridge_discards_oversized_lines_and_resynchronizes_at_the_next_request()
    {
        var root = TemporaryDirectory();
        try
        {
            var oversized = new string('x', 256 * 1024 + 1);
            using var input = new StringReader(
                oversized +
                "\n" +
                "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"ping\",\"params\":{\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientCapabilities\":{}}}}\n");
            using var output = new StringWriter(CultureInfo.InvariantCulture);

            Assert.Equal(
                0,
                await ObserverBridgeServer.RunAsync(
                    new ActiveWorkflowRunStore(root),
                    input,
                    output,
                    TestContext.Current.CancellationToken));

            var responses = output.ToString().Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, responses.Length);
            using var rejected = JsonDocument.Parse(responses[0]);
            Assert.Equal(-32600, rejected.RootElement.GetProperty("error").GetProperty("code").GetInt32());
            using var pong = JsonDocument.Parse(responses[1]);
            Assert.Equal(2, pong.RootElement.GetProperty("id").GetInt32());
            Assert.Equal("complete", pong.RootElement.GetProperty("result").GetProperty("resultType").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Capture_is_typed_deterministic_and_redacts_before_handoff()
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            using var document = JsonDocument.Parse(ValidArgumentsJson);
            Assert.True(
                DiagnosticSnapshotCapture.TryCreate(
                    ActiveRun(),
                    document.RootElement,
                    inbox.Protector,
                    s_timestamp,
                    out var request,
                    out var error),
                error.Code);

            Assert.NotNull(request);
            Assert.Equal("fail", request.Outcome);
            Assert.Equal(6, request.VariableCount);
            Assert.Equal(4, request.CheckCount);
            Assert.Equal(1, request.FailedCheckCount);
            Assert.DoesNotContain("sensitive-plain", request.Content.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-plain", request.Content.Content, StringComparison.Ordinal);

            using var captured = JsonDocument.Parse(request.Content.Content);
            var payload = captured.RootElement;
            Assert.Equal("qyl.agent.diagnostic.snapshot", payload.GetProperty("extension_id").GetString());
            Assert.Equal(1, payload.GetProperty("format_version").GetInt32());
            Assert.Matches("^[0-9a-f]{32}$", payload.GetProperty("capture_nonce").GetString()!);
            Assert.Equal(
                ["label", "limit", "missing", "password", "result", "token"],
                payload.GetProperty("variables")
                    .EnumerateArray()
                    .Select(static item => item.GetProperty("name").GetString()));

            var sensitive = payload.GetProperty("variables").EnumerateArray()
                .Single(static item => item.GetProperty("name").GetString() == "token");
            Assert.Equal("redacted", sensitive.GetProperty("capture").GetString());
            Assert.False(sensitive.TryGetProperty("value", out _));
            var secret = payload.GetProperty("variables").EnumerateArray()
                .Single(static item => item.GetProperty("name").GetString() == "password");
            Assert.Equal("omitted", secret.GetProperty("capture").GetString());
            Assert.False(secret.TryGetProperty("value", out _));
            var result = payload.GetProperty("variables").EnumerateArray()
                .Single(static item => item.GetProperty("name").GetString() == "result");
            Assert.Equal("integer", result.GetProperty("type").GetString());
            Assert.Equal("value", result.GetProperty("capture").GetString());

            var contentHash = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(request.Content.Content)));
            Assert.Equal($"sha256:{contentHash}", request.Content.ContentRef.Value);

            var normalizer = SeedNormalizer();
            var batch = normalizer.NormalizeDiagnosticSnapshot(request, s_timestamp);
            var workflowEvent = Assert.Single(batch.Events);
            Assert.Equal(WorkflowJournalEventKind.ContentCaptured, workflowEvent.Kind);
            Assert.Equal("thread-root", workflowEvent.ThreadId);
            Assert.Equal("turn-root", workflowEvent.TurnId);
            Assert.Equal("turn-root", workflowEvent.AttemptId?.Value);
            Assert.Equal(
                [
                    "check_count",
                    "content_ref",
                    "extension_id",
                    "failed_check_count",
                    "format_version",
                    "outcome",
                    "phase",
                    "probe_id",
                    "snapshot_id",
                    "variable_count"
                ],
                workflowEvent.Data!.Keys.Order(StringComparer.Ordinal));
            Assert.Equal(request.Content.ContentRef.Value, workflowEvent.Data["content_ref"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Tool_call_crosses_encrypted_inbox_and_reaches_encrypted_spool()
    {
        var root = TemporaryDirectory();
        try
        {
            var activeRuns = new ActiveWorkflowRunStore(root);
            await activeRuns.WriteAsync(
                ActiveRun(),
                TestContext.Current.CancellationToken);
            var inbox = new DiagnosticSnapshotInbox(root);
            inbox.PrepareRun("run-live");
            var spool = new WorkflowSpoolStore(root).Open("run-live");
            var normalizer = SeedNormalizer();
            using var journalGate = new SemaphoreSlim(1, 1);
            using var drainCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);
            var drain = Task.Run(
                async () =>
                {
                    try
                    {
                        while (!drainCancellation.IsCancellationRequested)
                        {
                            await CodexObserverRuntime.DrainDiagnosticsOnceAsync(
                                inbox,
                                "run-live",
                                normalizer,
                                spool,
                                null,
                                journalGate,
                                drainCancellation.Token);
                            await Task.Delay(10, drainCancellation.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                },
                drainCancellation.Token);

            var compactArguments = string.Concat(
                ValidArgumentsJson.Where(static character => character is not '\r' and not '\n'));
            var requestJson =
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"record_diagnostic_snapshot\",\"arguments\":" +
                compactArguments +
                ",\"_meta\":{\"io.modelcontextprotocol/protocolVersion\":\"2026-07-28\",\"io.modelcontextprotocol/clientCapabilities\":{}}}}\n";
            using var input = new StringReader(requestJson);
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            Assert.Equal(
                0,
                await ObserverBridgeServer.RunAsync(
                    activeRuns,
                    input,
                    output,
                    TestContext.Current.CancellationToken));
            await drainCancellation.CancelAsync();
            await drain;

            using var response = JsonDocument.Parse(output.ToString());
            var result = response.RootElement.GetProperty("result");
            Assert.False(result.GetProperty("isError").GetBoolean());
            Assert.True(result.GetProperty("structuredContent").GetProperty("recorded").GetBoolean());
            Assert.Equal("recorded", result.GetProperty("structuredContent").GetProperty("code").GetString());

            var entry = Assert.Single(spool.ReadAfter(0, 10));
            Assert.Equal(WorkflowJournalEventKind.ContentCaptured, entry.Event.Kind);
            Assert.Single(entry.Content);
            Assert.Contains("public-result", entry.Content[0].Content, StringComparison.Ordinal);
            Assert.Contains("sensitive-plain", ValidArgumentsJson, StringComparison.Ordinal);
            Assert.DoesNotContain("sensitive-plain", entry.Content[0].Content, StringComparison.Ordinal);
            Assert.DoesNotContain("secret-plain", entry.Content[0].Content, StringComparison.Ordinal);

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var disk = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(
                    path,
                    TestContext.Current.CancellationToken));
                Assert.DoesNotContain("sensitive-plain", disk, StringComparison.Ordinal);
                Assert.DoesNotContain("secret-plain", disk, StringComparison.Ordinal);
                Assert.DoesNotContain("public-result", disk, StringComparison.Ordinal);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Same_semantic_snapshot_is_idempotent_and_changed_snapshot_conflicts()
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            inbox.PrepareRun("run-live");
            var first = Capture(inbox, ValidArgumentsJson);
            var reordered = Capture(inbox, ReorderedArgumentsJson);
            Assert.Equal(first.PayloadDigest, reordered.PayloadDigest);

            var spool = new WorkflowSpoolStore(root).Open("run-live");
            var normalizer = SeedNormalizer();
            using var gate = new SemaphoreSlim(1, 1);
            var submission = inbox.SubmitAsync(
                first,
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            await WaitForPendingAsync(inbox, "run-live");
            Assert.Equal(
                1,
                await CodexObserverRuntime.DrainDiagnosticsOnceAsync(
                    inbox,
                    "run-live",
                    normalizer,
                    spool,
                    null,
                    gate,
                    TestContext.Current.CancellationToken));
            Assert.True((await submission).Recorded);

            var replay = await inbox.SubmitAsync(
                reordered,
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);
            Assert.True(replay.Recorded);
            Assert.Single(spool.ReadAfter(0, 10));

            using var changedDocument = JsonDocument.Parse(
                ValidArgumentsJson.Replace("public-result", "changed-result", StringComparison.Ordinal));
            Assert.True(DiagnosticSnapshotCapture.TryCreate(
                ActiveRun(),
                changedDocument.RootElement,
                inbox.Protector,
                s_timestamp,
                out var changed,
                out _));
            var conflict = await inbox.SubmitAsync(
                changed!,
                TimeSpan.FromSeconds(1),
                TestContext.Current.CancellationToken);
            Assert.False(conflict.Recorded);
            Assert.Equal("snapshot_conflict", conflict.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task New_run_generation_rejects_delayed_prior_run_submissions()
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            inbox.PrepareRun("run-live");
            var stale = Capture(inbox, ValidArgumentsJson);

            inbox.PrepareRun("run-next");
            var result = await inbox.SubmitAsync(
                stale,
                TimeSpan.FromMilliseconds(100),
                TestContext.Current.CancellationToken);

            Assert.False(result.Recorded);
            Assert.Equal("run_closing", result.Code);
            Assert.Empty(inbox.ReadPending("run-live"));
            Assert.Single(Directory.EnumerateFiles(
                Path.Combine(root, "diagnostic-inbox"),
                "*.lock",
                SearchOption.TopDirectoryOnly));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Unreadable_inbox_request_is_quarantined_without_blocking_the_run()
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            inbox.PrepareRun("run-live");
            var requestPath = Path.Combine(
                root,
                "diagnostic-inbox",
                "unreadable.request.qyl");
            await File.WriteAllTextAsync(
                requestPath,
                "{not-json",
                TestContext.Current.CancellationToken);

            Assert.Empty(inbox.ReadPending("run-live"));
            Assert.False(File.Exists(requestPath));
            Assert.True(File.Exists(requestPath + ".corrupt"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Ack_retry_does_not_duplicate_spool_or_telemetry_projection()
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            inbox.PrepareRun("run-live");
            var request = Capture(inbox, ValidArgumentsJson);
            var submission = inbox.SubmitAsync(
                request,
                TimeSpan.FromSeconds(2),
                TestContext.Current.CancellationToken);
            await WaitForPendingAsync(inbox, "run-live");

            var normalizer = SeedNormalizer();
            var batch = normalizer.NormalizeDiagnosticSnapshot(request, s_timestamp);
            var workflowEvent = Assert.Single(batch.Events);
            var spool = new WorkflowSpoolStore(root).Open("run-live");
            await spool.AppendAsync(
                new WorkflowSpoolEntry(workflowEvent, [request.Content]),
                TestContext.Current.CancellationToken);
            normalizer.MarkDiagnosticSnapshotRecorded(request.SnapshotId);

            var projected = 0;
            using var gate = new SemaphoreSlim(1, 1);
            Assert.Equal(
                1,
                await CodexObserverRuntime.DrainDiagnosticsOnceAsync(
                    inbox,
                    "run-live",
                    normalizer,
                    spool,
                    _ => projected++,
                    gate,
                    TestContext.Current.CancellationToken));
            Assert.True((await submission).Recorded);
            Assert.Equal(0, projected);
            Assert.Single(spool.ReadAfter(0, 10));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("duplicate-variable", "duplicate_variable")]
    [InlineData("bad-actual", "invalid_actual_variable")]
    [InlineData("expression", "invalid_check")]
    public void Invalid_dynamic_input_is_rejected_with_machine_codes(string fixture, string expectedCode)
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            var json = fixture switch
            {
                "duplicate-variable" =>
                    """{"snapshotId":"snapshot_1","probeId":"probe_1","phase":"input","variables":[{"name":"x","classification":"public","value":1},{"name":"x","classification":"internal","value":2}]}""",
                "bad-actual" =>
                    """{"snapshotId":"snapshot_1","probeId":"probe_1","phase":"input","variables":[],"checks":[{"checkId":"check_1","operator":"exists","actual":"not valid"}]}""",
                _ =>
                    """{"snapshotId":"snapshot_1","probeId":"probe_1","phase":"input","variables":[{"name":"x","classification":"public","value":1}],"checks":[{"checkId":"check_1","operator":"equal","actual":"x","expression":"x == 1"}]}"""
            };
            using var document = JsonDocument.Parse(json);

            Assert.False(DiagnosticSnapshotCapture.TryCreate(
                ActiveRun(),
                document.RootElement,
                inbox.Protector,
                s_timestamp,
                out _,
                out var error));
            Assert.Equal(expectedCode, error.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData("variables", "too_many_variables", "variables")]
    [InlineData("checks", "too_many_checks", "checks")]
    [InlineData("depth", "value_too_deep", "variables[0].value")]
    [InlineData("value", "value_too_large", "variables[0].value")]
    [InlineData("captured-payload", "payload_too_large", "arguments")]
    public void Capture_enforces_runtime_bounds(string fixture, string expectedCode, string expectedField)
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            var variables = fixture switch
            {
                "variables" => string.Join(
                    ',',
                    Enumerable.Range(0, DiagnosticSnapshotCapture.MaxVariables + 1)
                        .Select(static index =>
                            $"{{\"name\":\"v{index}\",\"classification\":\"public\",\"value\":{index}}}")),
                "depth" =>
                    "{\"name\":\"deep\",\"classification\":\"public\",\"value\":" +
                    new string('[', DiagnosticSnapshotCapture.MaxValueDepth + 1) +
                    "0" +
                    new string(']', DiagnosticSnapshotCapture.MaxValueDepth + 1) +
                    "}",
                "value" =>
                    "{\"name\":\"large\",\"classification\":\"public\",\"value\":" +
                    "\"" +
                    new string('x', DiagnosticSnapshotCapture.MaxValueBytes) +
                    "\"" +
                    "}",
                "captured-payload" => string.Join(
                    ',',
                    Enumerable.Range(0, 5).Select(index =>
                        $"{{\"name\":\"large{index}\",\"classification\":\"public\",\"value\":" +
                        "\"" +
                        new string((char)('a' + index), 15_000) +
                        "\"" +
                        "}")),
                _ => "{\"name\":\"x\",\"classification\":\"public\",\"value\":1}"
            };
            var checks = fixture == "checks"
                ? string.Join(
                    ',',
                    Enumerable.Range(0, DiagnosticSnapshotCapture.MaxChecks + 1)
                        .Select(static index =>
                            $"{{\"checkId\":\"c{index}\",\"operator\":\"exists\",\"actual\":\"x\"}}"))
                : "";
            var json =
                "{\"snapshotId\":\"snapshot_bounds\",\"probeId\":\"probe_bounds\",\"phase\":\"input\",\"variables\":[" +
                variables +
                "]" +
                (fixture == "checks" ? ",\"checks\":[" + checks + "]" : "") +
                "}";
            using var document = JsonDocument.Parse(json);

            Assert.False(DiagnosticSnapshotCapture.TryCreate(
                ActiveRun(),
                document.RootElement,
                inbox.Protector,
                s_timestamp,
                out _,
                out var error));
            Assert.Equal(expectedCode, error.Code);
            Assert.Equal(expectedField, error.Field);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Missing_operands_and_incompatible_types_are_unknown_without_expressions()
    {
        var root = TemporaryDirectory();
        try
        {
            var inbox = new DiagnosticSnapshotInbox(root);
            using var document = JsonDocument.Parse(
                """
                {
                  "snapshotId":"snapshot_unknown",
                  "probeId":"probe_unknown",
                  "phase":"input",
                  "variables":[
                    {"name":"text","classification":"public","value":"1"},
                    {"name":"number","classification":"public","value":1}
                  ],
                  "checks":[
                    {"checkId":"missing_operand","operator":"equal","actual":"missing","expected":"number"},
                    {"checkId":"incompatible","operator":"not_equal","actual":"text","expected":"number"},
                    {"checkId":"missing_exists","operator":"exists","actual":"missing"}
                  ]
                }
                """);

            Assert.True(DiagnosticSnapshotCapture.TryCreate(
                ActiveRun(),
                document.RootElement,
                inbox.Protector,
                s_timestamp,
                out var request,
                out var error), error.Code);
            Assert.Equal("fail", request!.Outcome);
            Assert.Equal(1, request.FailedCheckCount);
            using var captured = JsonDocument.Parse(request.Content.Content);
            var outcomes = captured.RootElement.GetProperty("checks")
                .EnumerateArray()
                .ToDictionary(
                    static check => check.GetProperty("check_id").GetString()!,
                    static check => check.GetProperty("outcome").GetString()!,
                    StringComparer.Ordinal);
            Assert.Equal("unknown", outcomes["missing_operand"]);
            Assert.Equal("unknown", outcomes["incompatible"]);
            Assert.Equal("fail", outcomes["missing_exists"]);
            Assert.Equal(2, outcomes.Values.Count(static outcome => outcome == "unknown"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Telemetry_projection_emits_only_fixed_diagnostic_and_workflow_tags()
    {
        var root = TemporaryDirectory();
        Activity? turnActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = static source => source.Name == "qyl.codex.observer",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                if (activity.DisplayName == "codex.workflow.turn")
                    turnActivity = activity;
            }
        };
        ActivitySource.AddActivityListener(listener);
        try
        {
            using var telemetry = WorkflowTelemetryProjection.Create("run-live", null);
            var normalizer = new CodexEventNormalizer();
            Record(telemetry, normalizer.StartRun(s_timestamp));
            using (var thread = JsonDocument.Parse(
                       """{"method":"thread/started","params":{"thread":{"id":"thread-root","createdAt":1786276800}}}"""))
            {
                Record(telemetry, normalizer.Normalize(thread.RootElement, s_timestamp));
            }
            using (var turn = JsonDocument.Parse(
                       """{"method":"turn/started","params":{"threadId":"thread-root","turn":{"id":"turn-root","startedAt":1786276800}}}"""))
            {
                Record(telemetry, normalizer.Normalize(turn.RootElement, s_timestamp));
            }

            var inbox = new DiagnosticSnapshotInbox(root);
            var request = Capture(inbox, ValidArgumentsJson);
            Record(telemetry, normalizer.NormalizeDiagnosticSnapshot(request, s_timestamp));
            telemetry.Record(new WorkflowEventAppend
            {
                EventId = new WorkflowEventId("other-content-extension"),
                SourceSequence = 100,
                Timestamp = s_timestamp,
                Kind = WorkflowJournalEventKind.ContentCaptured,
                ThreadId = "thread-root",
                TurnId = "turn-root",
                AttemptId = new WorkflowAttemptId("turn-root"),
                Data = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["extension_id"] = "qyl.other.extension",
                    ["format_version"] = 1
                }
            });

            Assert.NotNull(turnActivity);
            var diagnostic = Assert.Single(
                turnActivity.Events,
                static item => item.Name == "qyl.agent.diagnostic.snapshot");
            var tags = diagnostic.Tags.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal);
            Assert.Equal(
                [
                    "qyl.agent.diagnostic.check.count",
                    "qyl.agent.diagnostic.check.failed_count",
                    "qyl.agent.diagnostic.extension.id",
                    "qyl.agent.diagnostic.format.version",
                    "qyl.agent.diagnostic.outcome",
                    "qyl.agent.diagnostic.phase",
                    "qyl.agent.diagnostic.probe.id",
                    "qyl.agent.diagnostic.snapshot.id",
                    "qyl.agent.diagnostic.variable.count",
                    "qyl.workflow.attempt.id",
                    "qyl.workflow.event.id",
                    "qyl.workflow.run.id"
                ],
                tags.Keys.Order(StringComparer.Ordinal));
            Assert.Equal("qyl.agent.diagnostic.snapshot", tags["qyl.agent.diagnostic.extension.id"]);
            Assert.Equal(1L, tags["qyl.agent.diagnostic.format.version"]);
            Assert.Equal(6L, tags["qyl.agent.diagnostic.variable.count"]);
            Assert.Equal(4L, tags["qyl.agent.diagnostic.check.count"]);
            Assert.Equal(1L, tags["qyl.agent.diagnostic.check.failed_count"]);
            Assert.DoesNotContain(tags, static item =>
                item.Key.Contains("result", StringComparison.Ordinal) ||
                item.Value?.ToString()?.Contains("sensitive-plain", StringComparison.Ordinal) is true ||
                item.Value?.ToString()?.Contains("secret-plain", StringComparison.Ordinal) is true);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DiagnosticSnapshotInboxRequest Capture(
        DiagnosticSnapshotInbox inbox,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.True(DiagnosticSnapshotCapture.TryCreate(
            ActiveRun(),
            document.RootElement,
            inbox.Protector,
            s_timestamp,
            out var request,
            out var error), error.Code);
        return request!;
    }

    private static void Record(
        WorkflowTelemetryProjection telemetry,
        CodexNormalizedBatch batch)
    {
        foreach (var workflowEvent in batch.Events ?? [])
            telemetry.Record(workflowEvent);
    }

    private static CodexEventNormalizer SeedNormalizer()
    {
        var normalizer = new CodexEventNormalizer();
        normalizer.StartRun(s_timestamp);
        using (var thread = JsonDocument.Parse(
                   """{"method":"thread/started","params":{"thread":{"id":"thread-root","createdAt":1786276800}}}"""))
        {
            normalizer.Normalize(thread.RootElement, s_timestamp);
        }
        using (var turn = JsonDocument.Parse(
                   """{"method":"turn/started","params":{"threadId":"thread-root","turn":{"id":"turn-root","startedAt":1786276800}}}"""))
        {
            normalizer.Normalize(turn.RootElement, s_timestamp);
        }
        return normalizer;
    }

    private static async Task WaitForPendingAsync(DiagnosticSnapshotInbox inbox, string runId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (inbox.ReadPending(runId).Count == 0)
            await Task.Delay(10, timeout.Token);
    }

    private static ActiveWorkflowRun ActiveRun() =>
        new("run-live", "thread-root", s_timestamp, Environment.ProcessId);

    private static string TemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qyl-diagnostic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private const string ValidArgumentsJson =
        """
        {
          "snapshotId":"snapshot_1",
          "probeId":"probe.output",
          "phase":"checkpoint",
          "variables":[
            {"name":"result","classification":"public","value":5},
            {"name":"label","classification":"public","value":"public-result"},
            {"name":"limit","classification":"internal","value":3},
            {"name":"token","classification":"sensitive","value":"sensitive-plain"},
            {"name":"password","classification":"secret","value":"secret-plain"},
            {"name":"missing","classification":"public","value":null}
          ],
          "checks":[
            {"checkId":"check_gt","operator":"greater_than","actual":"result","expected":"limit"},
            {"checkId":"check_type","operator":"type_is","actual":"result","expectedType":"integer"},
            {"checkId":"check_token","operator":"contains","actual":"token","expected":"token"},
            {"checkId":"check_exists","operator":"exists","actual":"missing"}
          ]
        }
        """;

    private const string ReorderedArgumentsJson =
        """
        {
          "probeId":"probe.output",
          "snapshotId":"snapshot_1",
          "variables":[
            {"classification":"secret","value":"secret-plain","name":"password"},
            {"classification":"public","value":null,"name":"missing"},
            {"classification":"internal","value":3,"name":"limit"},
            {"classification":"sensitive","value":"sensitive-plain","name":"token"},
            {"classification":"public","value":5,"name":"result"}
            ,{"classification":"public","value":"public-result","name":"label"}
          ],
          "phase":"checkpoint",
          "checks":[
            {"actual":"missing","operator":"exists","checkId":"check_exists"},
            {"expected":"token","actual":"token","operator":"contains","checkId":"check_token"},
            {"expectedType":"integer","actual":"result","operator":"type_is","checkId":"check_type"},
            {"expected":"limit","actual":"result","operator":"greater_than","checkId":"check_gt"}
          ]
        }
        """;
}
