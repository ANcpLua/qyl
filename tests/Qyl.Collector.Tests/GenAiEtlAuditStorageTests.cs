using System.Text.Json;
using Qyl.Collector.Ingestion;
using Qyl.Collector.Primitives;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Tests;

public sealed class GenAiEtlAuditStorageTests
{
    [Fact]
    public async Task Audit_rows_aggregate_leaf_calls_and_exclude_only_proven_agent_rollups()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);

        await store.EnqueueAsync(new SpanBatch(
        [
            Row("rollup", "trace-a", "rollup", startNano + 1, operation: "invoke_agent", input: 30,
                output: 12),
            Row("chat-a", "trace-a", "chat-a", startNano + 2, parentSpanId: "rollup", durationMs: 10,
                status: 0, operation: "chat", outputType: "json", provider: "openai", model: "gpt-test",
                input: 10, output: 4, cacheRead: 2, cacheCreate: 1, reasoning: 3),
            Row("chat-b", "trace-b", "chat-b", startNano + 3, durationMs: 30, status: 2,
                operation: "chat", outputType: "json", provider: "openai", model: "gpt-test",
                input: 20, output: 8, cacheRead: 3, cacheCreate: 2, reasoning: 4),
            // A standalone invoke_agent has no GenAI child, so the query must not erase it by name.
            Row("agent-only", "trace-c", "agent-only", startNano + 4, operation: "invoke_agent",
                provider: "anthropic", model: "claude-test", input: 5, output: 2),
            Row("http", "trace-d", "http", startNano + 5)
        ]), TestContext.Current.CancellationToken);

        var rows = await store.GetGenAiEtlAuditRowsAsync(
            "default",
            periodStart,
            periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        var chat = Assert.Single(rows, row => row.OperationName == "chat");
        Assert.Equal("workload", chat.ServiceName);
        Assert.Equal("json", chat.OutputType);
        Assert.Equal("openai", chat.ProviderName);
        Assert.Equal("gpt-test", chat.ModelName);
        Assert.Equal("request_model_fallback", chat.ModelIdentityBasis);
        Assert.Equal(2, chat.CallCount);
        Assert.Equal(30, chat.InputTokens);
        Assert.Equal(12, chat.OutputTokens);
        Assert.Equal(5, chat.CacheReadInputTokens);
        Assert.Equal(3, chat.CacheCreationInputTokens);
        Assert.Equal(7, chat.ReasoningOutputTokens);
        Assert.Equal(2, chat.TokenUsageCallCount);
        Assert.Equal(1, chat.ErrorCount);
        Assert.Equal(20, chat.AverageLatencyMs, 6);
        Assert.Equal(29, chat.P95LatencyMs, 6);

        var standaloneAgent = Assert.Single(rows, row => row.OperationName == "invoke_agent");
        Assert.Equal(1, standaloneAgent.CallCount);
        Assert.Equal(5, standaloneAgent.InputTokens);
        Assert.Equal(2, standaloneAgent.OutputTokens);
    }

    [Fact]
    public async Task Audit_snapshot_usage_buckets_preserve_per_call_vectors_and_missing_vs_zero()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);

        await store.EnqueueAsync(new SpanBatch(
        [
            Row("base-a", "trace-a", "base-a", startNano + 1, operation: "chat", outputType: "text",
                provider: "openai", model: "gpt-test", input: 600, output: 10),
            Row("base-b", "trace-b", "base-b", startNano + 2, operation: "chat", outputType: "text",
                provider: "openai", model: "gpt-test", input: 600, output: 10),
            Row("explicit-zero", "trace-c", "explicit-zero", startNano + 3, operation: "chat",
                outputType: "text", provider: "openai", model: "gpt-test", input: 600, output: 10,
                cacheRead: 0, cacheCreate: 0, reasoning: 0),
            Row("above-tier", "trace-d", "above-tier", startNano + 4, operation: "chat",
                outputType: "text", provider: "openai", model: "gpt-test", input: 1200, output: 10)
        ]), TestContext.Current.CancellationToken);

        var snapshot = await store.GetGenAiEtlAuditSnapshotAsync(
            "default",
            periodStart,
            periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(4, Assert.Single(snapshot.AuditRows).CallCount);
        Assert.Equal(3, snapshot.UsageBuckets.Count);

        var baseBucket = Assert.Single(snapshot.UsageBuckets, bucket =>
            bucket.InputTokens == 600 && bucket.CacheReadInputTokens is null);
        Assert.Equal(2, baseBucket.CallCount);
        Assert.Null(baseBucket.CacheCreationInputTokens);
        Assert.Null(baseBucket.ReasoningOutputTokens);
        Assert.Equal(GenAiEtlAuditUsageEligibility.Eligible, baseBucket.Eligibility);

        var explicitZero = Assert.Single(snapshot.UsageBuckets, bucket =>
            bucket.InputTokens == 600 && bucket.CacheReadInputTokens == 0);
        Assert.Equal(1, explicitZero.CallCount);
        Assert.Equal(0, explicitZero.CacheCreationInputTokens);
        Assert.Equal(0, explicitZero.ReasoningOutputTokens);
        Assert.Equal(GenAiEtlAuditUsageEligibility.Eligible, explicitZero.Eligibility);

        var aboveTier = Assert.Single(snapshot.UsageBuckets, bucket => bucket.InputTokens == 1200);
        Assert.Equal(1, aboveTier.CallCount);
        Assert.Equal(GenAiEtlAuditUsageEligibility.Eligible, aboveTier.Eligibility);
    }

    [Fact]
    public async Task Audit_snapshot_usage_bucket_eligibility_is_operation_aware_and_keeps_invalid_evidence()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);

        await store.EnqueueAsync(new SpanBatch(
        [
            Row("rollup", "trace-rollup", "rollup", startNano + 1, operation: "invoke_agent",
                model: "excluded-rollup", input: 20, output: 8),
            Row("rollup-child", "trace-rollup", "rollup-child", startNano + 2, parentSpanId: "rollup",
                operation: "chat", model: "valid-chat", input: 10, output: 4),
            Row("missing-chat-output", "trace-a", "missing-chat-output", startNano + 3,
                operation: "chat", model: "missing-chat-output", input: 10),
            Row("valid-embedding", "trace-b", "valid-embedding", startNano + 4,
                operation: "embeddings", model: "valid-embedding", input: 10),
            Row("missing-embedding-input", "trace-c", "missing-embedding-input", startNano + 5,
                operation: "embeddings", model: "missing-embedding-input"),
            Row("negative-cache", "trace-d", "negative-cache", startNano + 6,
                operation: "generate_content", model: "negative-cache", input: 10, output: 4,
                cacheRead: -1),
            Row("cache-overflow", "trace-e", "cache-overflow", startNano + 7,
                operation: "text_completion", model: "cache-overflow", input: 10, output: 4,
                cacheRead: 8, cacheCreate: 3),
            Row("reasoning-overflow", "trace-f", "reasoning-overflow", startNano + 8,
                operation: "chat", model: "reasoning-overflow", input: 10, output: 4, reasoning: 5),
            Row("standalone-agent", "trace-g", "standalone-agent", startNano + 9,
                operation: "invoke_agent", model: "standalone-agent", input: 10, output: 4)
        ]), TestContext.Current.CancellationToken);

        var buckets = (await store.GetGenAiEtlAuditSnapshotAsync(
                "default",
                periodStart,
                periodStart.AddDays(1),
                TestContext.Current.CancellationToken))
            .UsageBuckets;

        Assert.DoesNotContain(buckets, bucket => bucket.ModelName == "excluded-rollup");
        Assert.Equal(8, buckets.Count);
        Assert.Equal(
            GenAiEtlAuditUsageEligibility.Eligible,
            Assert.Single(buckets, bucket => bucket.ModelName == "valid-chat").Eligibility);
        Assert.Equal(
            GenAiEtlAuditUsageEligibility.MissingRequiredUsage,
            Assert.Single(buckets, bucket => bucket.ModelName == "missing-chat-output").Eligibility);

        var embedding = Assert.Single(buckets, bucket => bucket.ModelName == "valid-embedding");
        Assert.Null(embedding.OutputTokens);
        Assert.Equal(GenAiEtlAuditUsageEligibility.Eligible, embedding.Eligibility);
        Assert.Equal(
            GenAiEtlAuditUsageEligibility.MissingRequiredUsage,
            Assert.Single(buckets, bucket => bucket.ModelName == "missing-embedding-input").Eligibility);

        Assert.Equal(
            GenAiEtlAuditUsageEligibility.InvalidUsage,
            Assert.Single(buckets, bucket => bucket.ModelName == "negative-cache").Eligibility);
        Assert.Equal(
            GenAiEtlAuditUsageEligibility.InvalidUsage,
            Assert.Single(buckets, bucket => bucket.ModelName == "cache-overflow").Eligibility);
        Assert.Equal(
            GenAiEtlAuditUsageEligibility.InvalidUsage,
            Assert.Single(buckets, bucket => bucket.ModelName == "reasoning-overflow").Eligibility);
        Assert.Equal(
            GenAiEtlAuditUsageEligibility.UnsupportedOperation,
            Assert.Single(buckets, bucket => bucket.ModelName == "standalone-agent").Eligibility);
    }

    [Fact]
    public async Task Audit_rows_are_project_and_half_open_period_scoped()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);
        var endNano = TimeConversions.ToUnixNanoUnsigned(periodStart.AddDays(1));

        await store.EnqueueAsync(new SpanBatch(
        [
            Row("inside", "trace-a", "inside", startNano, operation: "chat", provider: "openai"),
            Row("outside-end", "trace-b", "outside-end", endNano, operation: "chat", provider: "openai"),
            Row("other-project", "trace-c", "other-project", startNano + 1, projectId: "other",
                operation: "chat", provider: "openai")
        ]), TestContext.Current.CancellationToken);

        var rows = await store.GetGenAiEtlAuditRowsAsync(
            "default",
            periodStart,
            periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal(1, row.CallCount);
    }

    [Fact]
    public async Task Audit_rows_prefer_the_exact_response_model_and_preserve_its_identity()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);

        await store.EnqueueAsync(new SpanBatch(
        [
            Row(
                "routed",
                "trace-routed",
                "routed",
                startNano,
                operation: "chat",
                provider: "openai",
                model: "gpt-requested",
                responseModel: "gpt-Actual-2026-07-01",
                input: 10,
                output: 4)
        ]), TestContext.Current.CancellationToken);

        var rows = await store.GetGenAiEtlAuditRowsAsync(
            "default",
            periodStart,
            periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal("gpt-Actual-2026-07-01", row.ModelName);
        Assert.Equal("response_model", row.ModelIdentityBasis);
    }

    [Fact]
    public async Task Audit_rows_do_not_mark_invalid_token_subsets_as_priceable_usage()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);

        await store.EnqueueAsync(new SpanBatch(
        [
            Row("valid", "trace-valid", "valid", startNano, operation: "chat", provider: "openai",
                model: "gpt-test", input: 10, output: 4, cacheRead: 2, cacheCreate: 1, reasoning: 1),
            Row("negative", "trace-negative", "negative", startNano + 1, operation: "chat",
                provider: "openai", model: "gpt-test", input: -1, output: 4),
            Row("cache-overflow", "trace-cache", "cache-overflow", startNano + 2, operation: "chat",
                provider: "openai", model: "gpt-test", input: 10, output: 4, cacheRead: 8, cacheCreate: 3),
            Row("reasoning-overflow", "trace-reasoning", "reasoning-overflow", startNano + 3,
                operation: "chat", provider: "openai", model: "gpt-test", input: 10, output: 4,
                reasoning: 5)
        ]), TestContext.Current.CancellationToken);

        var rows = await store.GetGenAiEtlAuditRowsAsync(
            "default",
            periodStart,
            periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        var row = Assert.Single(rows);
        Assert.Equal(4, row.CallCount);
        Assert.Equal(1, row.TokenUsageCallCount);
    }

    [Fact]
    public async Task Audit_snapshot_reads_telemetry_cost_buckets_and_sync_state_together()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddDays(1);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);
        var scopeKey = Qyl.Collector.Cost.ProviderCostScope
            .ForIdentifier("proj-qyl")
            .CreateStableKey("openai");

        await store.EnqueueAsync(new SpanBatch(
        [
            Row("chat", "trace-a", "chat", startNano, operation: "chat", provider: "openai")
        ]), TestContext.Current.CancellationToken);
        await store.ReplaceProviderCostBucketsAsync(
            "default",
            "openai",
            periodStart,
            periodEnd,
            [
                new ProviderCostBucketRow
                {
                    ProjectId = "default",
                    Provider = "openai",
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    ModelKey = "*",
                    SourceEndpoint = "https://api.openai.com/v1/organization/costs",
                    ProviderScopeKey = scopeKey,
                    SourceKind = "actual_billed_cost",
                    Attribution = "provider_period",
                    CurrencyCode = "USD",
                    Amount = 2.5m,
                    RetrievedAt = periodEnd
                }
            ],
            new ProviderCostSyncRow
            {
                ProjectId = "default",
                Provider = "openai",
                SourceEndpoint = "https://api.openai.com/v1/organization/costs",
                ProviderScopeKey = scopeKey,
                SourceKind = "actual_billed_cost",
                Attribution = "provider_period",
                Status = "current",
                LastAttemptAt = periodEnd,
                LastSuccessAt = periodEnd,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            },
            TestContext.Current.CancellationToken);

        var snapshot = await store.GetGenAiEtlAuditSnapshotAsync(
            "default",
            periodStart,
            periodEnd,
            TestContext.Current.CancellationToken);

        Assert.Single(snapshot.AuditRows);
        Assert.Equal(2.5m, Assert.Single(snapshot.CostBuckets).Amount);
        Assert.Equal(scopeKey, Assert.Single(snapshot.CostSyncRows).ProviderScopeKey);
    }

    [Fact]
    public async Task Audit_rows_collapse_unapproved_operation_and_output_values_to_unknown_dimensions()
    {
        await using var store = new DuckDbStore(":memory:");
        var periodStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var startNano = TimeConversions.ToUnixNanoUnsigned(periodStart);

        await store.EnqueueAsync(new SpanBatch(
        [
            IngestedRow(
                "valid",
                "trace-valid",
                startNano + 1,
                operation: " CHAT ",
                outputType: " JSON ",
                provider: "openai",
                model: "gpt-test"),
            IngestedRow(
                "private-a",
                "trace-private-a",
                startNano + 2,
                operation: "patient: alice@example.com",
                outputType: "ssn: 123-45-6789",
                provider: "openai",
                model: "gpt-test"),
            IngestedRow(
                "private-b",
                "trace-private-b",
                startNano + 3,
                operation: "patient: bob@example.com",
                outputType: "account: secret-42",
                provider: "openai",
                model: "gpt-test")
        ]), TestContext.Current.CancellationToken);

        var rows = await store.GetGenAiEtlAuditRowsAsync(
            "default",
            periodStart,
            periodStart.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, rows.Count);
        var standard = Assert.Single(rows, row => row.OperationName == "chat");
        Assert.Equal("json", standard.OutputType);
        Assert.Equal(1, standard.CallCount);

        var unknown = Assert.Single(rows, row => row.OperationName is null);
        Assert.Null(unknown.OutputType);
        Assert.Equal(2, unknown.CallCount);

        var stored = await store.GetSpansAsync(
            "default",
            ct: TestContext.Current.CancellationToken);
        foreach (var span in stored.Where(static span => span.SpanId.StartsWith("private-", StringComparison.Ordinal)))
        {
            using var attributes = JsonDocument.Parse(Assert.IsType<string>(span.AttributesJson));
            Assert.False(attributes.RootElement.TryGetProperty(
                CollectorSemanticAttributeCatalog.GenAiOperationName,
                out _));
            Assert.False(attributes.RootElement.TryGetProperty(
                CollectorSemanticAttributeCatalog.GenAiOutputType,
                out _));
        }

        var valid = Assert.Single(stored, static span => span.SpanId == "valid");
        using var validAttributes = JsonDocument.Parse(Assert.IsType<string>(valid.AttributesJson));
        Assert.Equal(
            "chat",
            validAttributes.RootElement
                .GetProperty(CollectorSemanticAttributeCatalog.GenAiOperationName)
                .GetString());
        Assert.Equal(
            "json",
            validAttributes.RootElement
                .GetProperty(CollectorSemanticAttributeCatalog.GenAiOutputType)
                .GetString());
    }

    [Fact]
    public async Task Duplicate_reingestion_updates_all_gen_ai_hot_dimensions_together()
    {
        await using var store = new DuckDbStore(":memory:");
        var startNano = TimeConversions.ToUnixNanoUnsigned(
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero));

        await store.EnqueueAsync(new SpanBatch(
        [
            IngestedRow(
                "replayed",
                "trace-replayed",
                startNano,
                operation: "chat",
                outputType: "text",
                provider: "openai",
                model: "gpt-old",
                inputTokens: 10)
        ]), TestContext.Current.CancellationToken);

        await store.EnqueueAsync(new SpanBatch(
        [
            IngestedRow(
                "replayed",
                "trace-replayed",
                startNano,
                operation: "generate_content",
                outputType: "image",
                provider: "google",
                model: "gemini-new",
                responseModel: "gemini-new-routed",
                inputTokens: 20)
        ]), TestContext.Current.CancellationToken);

        var span = Assert.Single(await store.GetTraceAsync(
            "trace-replayed",
            "default",
            TestContext.Current.CancellationToken));
        Assert.Equal("google", span.GenAiProviderName);
        Assert.Equal("generate_content", span.GenAiOperationName);
        Assert.Equal("image", span.GenAiOutputType);
        Assert.Equal("gemini-new", span.GenAiRequestModel);
        Assert.Equal("gemini-new-routed", span.GenAiResponseModel);
        Assert.Equal(20, span.GenAiInputTokens);
    }

    private static SpanStorageRow IngestedRow(
        string spanId,
        string traceId,
        ulong startNano,
        string? operation,
        string? outputType,
        string? provider,
        string? model,
        string? responseModel = null,
        long? inputTokens = null)
    {
        var attributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal);
        AddString(CollectorSemanticAttributeCatalog.GenAiOperationName, operation);
        AddString(CollectorSemanticAttributeCatalog.GenAiOutputType, outputType);
        AddString(CollectorSemanticAttributeCatalog.GenAiProviderName, provider);
        AddString(CollectorSemanticAttributeCatalog.GenAiRequestModel, model);
        AddString(CollectorSemanticAttributeCatalog.GenAiResponseModel, responseModel);
        if (inputTokens.HasValue)
        {
            attributes.Add(
                CollectorSemanticAttributeCatalog.GenAiInputTokens,
                OtlpAttributeValue.FromInt(inputTokens.Value));
        }

        var row = Assert.Single(IngestionStorageMapper.ToSpanStorageRows(new TraceIngestionBatch(
        [
            new SpanIngestionRecord
            {
                SpanId = spanId,
                TraceId = traceId,
                Name = "gen-ai",
                Kind = 3,
                StartTimeUnixNano = startNano,
                EndTimeUnixNano = startNano + 1_000_000,
                StatusCode = 0,
                ServiceName = "workload",
                Attributes = attributes,
                ResourceAttributes = new Dictionary<string, OtlpAttributeValue>(StringComparer.Ordinal)
            }
        ])));
        return row;

        void AddString(string key, string? value)
        {
            if (value is not null)
                attributes.Add(key, OtlpAttributeValue.FromString(value));
        }
    }

    private static SpanStorageRow Row(
        string spanId,
        string traceId,
        string name,
        ulong startNano,
        string projectId = "default",
        string? parentSpanId = null,
        double durationMs = 1,
        byte status = 0,
        string? operation = null,
        string? outputType = null,
        string? provider = null,
        string? model = null,
        string? responseModel = null,
        long? input = null,
        long? output = null,
        long? cacheRead = null,
        long? cacheCreate = null,
        long? reasoning = null) =>
        new()
        {
            ProjectId = projectId,
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            Name = name,
            Kind = 3,
            StartTimeUnixNano = startNano,
            EndTimeUnixNano = startNano + (ulong)(durationMs * 1_000_000),
            DurationNs = (ulong)(durationMs * 1_000_000),
            StatusCode = status,
            ServiceName = "workload",
            GenAiOperationName = operation,
            GenAiOutputType = outputType,
            GenAiProviderName = provider,
            GenAiRequestModel = model,
            GenAiResponseModel = responseModel,
            GenAiInputTokens = input,
            GenAiOutputTokens = output,
            GenAiCacheReadInputTokens = cacheRead,
            GenAiCacheCreationInputTokens = cacheCreate,
            GenAiReasoningTokens = reasoning
        };
}
