using qyl.collector.Storage;

namespace qyl.collector.tests.Helpers;

/// <summary>
///     Builder state for creating SpanStorageRow instances.
///     Uses mutable internal state, then creates immutable SpanStorageRow via Build().
/// </summary>
internal sealed class SpanBuilder
{
    private static int _sCounter;

    // Data
    private string? _attributesJson;
    private ulong _durationNs;
    private ulong _endTimeUnixNano;
    private double? _genAiCostUsd;
    private long? _genAiInputTokens;
    private long? _genAiOutputTokens;
    private string? _genAiRequestModel;
    private string? _genAiResponseModel;
    private string? _genAiStopReason;

    // GenAI fields
    private string? _genAiSystem;
    private double? _genAiTemperature;
    private string? _genAiToolCallId;
    private string? _genAiToolName;
    private byte _kind;
    private string _name = TestConstants.OperationDefault;

    // Optional fields
    private string? _parentSpanId;
    private string? _resourceJson;
    private string? _serviceName;
    private string? _sessionId;
    private string _spanId = "span-000001";
    private ulong _startTimeUnixNano;
    private byte _statusCode;
    private string? _statusMessage;

    // Required fields
    private string _traceId = "trace-000001";

    private SpanBuilder()
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        SetTiming(now, TestConstants.DurationDefaultMs);
    }

    /// <summary>Creates a new SpanBuilder with auto-generated IDs.</summary>
    public static SpanBuilder Create()
    {
        var id = Interlocked.Increment(ref _sCounter);
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var builder = new SpanBuilder
        {
            _traceId = $"trace-{id:D6}",
            _spanId = $"span-{id:D6}",
            _name = $"operation-{id}"
        };
        builder.SetTiming(now, TestConstants.DurationDefaultMs);
        return builder;
    }

    /// <summary>Creates a SpanBuilder with explicit trace and span IDs.</summary>
    public static SpanBuilder Create(string traceId, string spanId)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var builder = new SpanBuilder
        {
            _traceId = traceId,
            _spanId = spanId,
            _name = TestConstants.OperationDefault
        };
        builder.SetTiming(now, TestConstants.DurationDefaultMs);
        return builder;
    }

    /// <summary>Creates a minimal span with only required fields.</summary>
    public static SpanBuilder Minimal(string traceId, string spanId)
    {
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;
        var builder = new SpanBuilder
        {
            _traceId = traceId,
            _spanId = spanId,
            _name = TestConstants.OperationMinimal
        };
        builder.SetTiming(now, TestConstants.DurationShortMs);
        return builder;
    }

    /// <summary>Creates a GenAI span with provider, model, and token data.</summary>
    public static SpanBuilder GenAi(string traceId, string spanId) =>
        Create(traceId, spanId)
            .WithProvider(TestConstants.ProviderOpenAi)
            .WithModel(TestConstants.ModelGpt4)
            .WithTokens(TestConstants.TokensInDefault, TestConstants.TokensOutDefault)
            .WithCost(TestConstants.CostDefault);

    // Identity
    public SpanBuilder WithTraceId(string traceId)
    {
        _traceId = traceId;
        return this;
    }

    public SpanBuilder WithSpanId(string spanId)
    {
        _spanId = spanId;
        return this;
    }

    public SpanBuilder WithParentSpanId(string? parentSpanId)
    {
        _parentSpanId = parentSpanId;
        return this;
    }

    public SpanBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    // Timing - internal helper to set all timing fields from DateTime
    private void SetTiming(DateTime startTime, double durationMs)
    {
        _startTimeUnixNano = DateTimeToUnixNano(startTime);
        var durationNs = (ulong)(durationMs * 1_000_000);
        _durationNs = durationNs;
        _endTimeUnixNano = _startTimeUnixNano + durationNs;
    }

    public SpanBuilder WithTiming(DateTime startTime, double durationMs)
    {
        SetTiming(startTime, durationMs);
        return this;
    }

    public SpanBuilder WithStartTime(DateTime startTime)
    {
        var durationNs = _durationNs;
        _startTimeUnixNano = DateTimeToUnixNano(startTime);
        _endTimeUnixNano = _startTimeUnixNano + durationNs;
        return this;
    }

    public SpanBuilder WithEndTime(DateTime endTime)
    {
        _endTimeUnixNano = DateTimeToUnixNano(endTime);
        if (_endTimeUnixNano > _startTimeUnixNano)
            _durationNs = _endTimeUnixNano - _startTimeUnixNano;
        return this;
    }

    public SpanBuilder AtTime(DateTime baseTime, int offsetMs = 0, double durationMs = TestConstants.DurationDefaultMs)
    {
        var start = baseTime.AddMilliseconds(offsetMs);
        SetTiming(start, durationMs);
        return this;
    }

    // Context
    public SpanBuilder WithSessionId(string? sessionId)
    {
        _sessionId = sessionId;
        return this;
    }

    public SpanBuilder WithServiceName(string? serviceName)
    {
        _serviceName = serviceName;
        return this;
    }

    // Status
    public SpanBuilder WithKind(byte kind)
    {
        _kind = kind;
        return this;
    }

    public SpanBuilder WithKind(string? kind)
    {
        _kind = kind switch
        {
            "internal" => 1,
            "server" => 2,
            "client" => 3,
            "producer" => 4,
            "consumer" => 5,
            _ => 0
        };
        return this;
    }

    public SpanBuilder WithStatusCode(byte statusCode)
    {
        _statusCode = statusCode;
        return this;
    }

    public SpanBuilder WithStatusCode(int? statusCode)
    {
        _statusCode = statusCode switch
        {
            1 => 1, // OK
            2 => 2, // ERROR
            _ => 0 // UNSET
        };
        return this;
    }

    public SpanBuilder WithStatusMessage(string? message)
    {
        _statusMessage = message;
        return this;
    }

    // GenAI
    public SpanBuilder WithProvider(string? provider)
    {
        _genAiSystem = provider;
        return this;
    }

    public SpanBuilder WithModel(string? model)
    {
        _genAiRequestModel = model;
        return this;
    }

    public SpanBuilder WithResponseModel(string? model)
    {
        _genAiResponseModel = model;
        return this;
    }

    public SpanBuilder WithTokens(long? input, long? output)
    {
        _genAiInputTokens = input;
        _genAiOutputTokens = output;
        return this;
    }

    public SpanBuilder WithCost(double? cost)
    {
        _genAiCostUsd = cost;
        return this;
    }

    public SpanBuilder WithTemperature(double? temperature)
    {
        _genAiTemperature = temperature;
        return this;
    }

    public SpanBuilder WithStopReason(string? reason)
    {
        _genAiStopReason = reason;
        return this;
    }

    public SpanBuilder WithToolCall(string? toolName, string? toolCallId)
    {
        _genAiToolName = toolName;
        _genAiToolCallId = toolCallId;
        return this;
    }

    // Data
    public SpanBuilder WithAttributes(string? attributesJson)
    {
        _attributesJson = attributesJson;
        return this;
    }

    public SpanBuilder WithResource(string? resourceJson)
    {
        _resourceJson = resourceJson;
        return this;
    }

    // Legacy compatibility - EvalScore/EvalReason not in new schema
    // These are no-ops for backward compatibility with existing tests
    public SpanBuilder WithEval(float? score, string? reason = null) =>
        // EvalScore and EvalReason are not in the new SpanStorageRow schema
        // Tests using these should be updated or removed
        this;

    public SpanBuilder WithEvents(string? events) =>
        // Events are not in the new SpanStorageRow schema
        // Tests using these should be updated or removed
        this;

    /// <summary>Builds the SpanStorageRow using object initializer.</summary>
    public SpanStorageRow Build() =>
        new()
        {
            TraceId = _traceId,
            SpanId = _spanId,
            ParentSpanId = _parentSpanId,
            SessionId = _sessionId,
            Name = _name,
            Kind = _kind,
            StartTimeUnixNano = _startTimeUnixNano,
            EndTimeUnixNano = _endTimeUnixNano,
            DurationNs = _durationNs,
            StatusCode = _statusCode,
            StatusMessage = _statusMessage,
            ServiceName = _serviceName,
            GenAiSystem = _genAiSystem,
            GenAiRequestModel = _genAiRequestModel,
            GenAiResponseModel = _genAiResponseModel,
            GenAiInputTokens = _genAiInputTokens,
            GenAiOutputTokens = _genAiOutputTokens,
            GenAiTemperature = _genAiTemperature,
            GenAiStopReason = _genAiStopReason,
            GenAiToolName = _genAiToolName,
            GenAiToolCallId = _genAiToolCallId,
            GenAiCostUsd = _genAiCostUsd,
            AttributesJson = _attributesJson,
            ResourceJson = _resourceJson
        };

    /// <summary>Builds and wraps in a SpanBatch.</summary>
    public SpanBatch ToBatch() => new([Build()]);

    /// <summary>Implicit conversion to SpanStorageRow.</summary>
    public static implicit operator SpanStorageRow(SpanBuilder builder) => builder.Build();

    /// <summary>Converts DateTime to Unix nanoseconds (ulong).</summary>
    private static ulong DateTimeToUnixNano(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
        var ticks = utc.Ticks - DateTime.UnixEpoch.Ticks;
        return (ulong)(ticks * 100); // 1 tick = 100 nanoseconds
    }
}

/// <summary>
///     Factory for creating multiple related spans.
/// </summary>
internal static class SpanFactory
{
    /// <summary>Creates a batch of spans with sequential IDs.</summary>
    public static SpanBatch CreateBatch(
        string traceId,
        string sessionId,
        int count,
        DateTime baseTime,
        int intervalMs = 10)
    {
        var spans = new List<SpanStorageRow>(count);
        for (var i = 0; i < count; i++)
            spans.Add(SpanBuilder.Create(traceId, $"span-{i:D3}")
                .WithSessionId(sessionId)
                .AtTime(baseTime, i * intervalMs, intervalMs / 2.0)
                .Build());
        return new SpanBatch(spans);
    }

    /// <summary>Creates a trace hierarchy with root and children.</summary>
    public static SpanBatch CreateHierarchy(
        string traceId,
        DateTime baseTime,
        int childCount = 2)
    {
        var spans = new List<SpanStorageRow>
        {
            SpanBuilder.Create(traceId, TestConstants.SpanRoot)
                .WithName("root")
                .AtTime(baseTime)
                .WithParentSpanId(null)
                .Build()
        };

        for (var i = 0; i < childCount; i++)
            spans.Add(SpanBuilder.Create(traceId, $"child{i + 1}")
                .WithName($"child{i + 1}")
                .AtTime(baseTime, (i + 1) * 10, 30)
                .WithParentSpanId(TestConstants.SpanRoot)
                .Build());

        return new SpanBatch(spans);
    }

    /// <summary>Creates spans for archive testing (old + new).</summary>
    public static SpanBatch CreateArchiveTestData(
        string sessionId,
        DateTime now,
        int oldDays = TestConstants.ArchiveDaysOld)
    {
        var oldTime = now.AddDays(-oldDays);
        return new SpanBatch(
        [
            SpanBuilder.Create("trace-archive-old", "span-old")
                .WithName("old")
                .WithSessionId(sessionId)
                .AtTime(oldTime)
                .Build(),
            SpanBuilder.Create("trace-archive-new", "span-new")
                .WithName("new")
                .WithSessionId(sessionId)
                .AtTime(now)
                .Build()
        ]);
    }

    /// <summary>Creates GenAI spans for stats testing.</summary>
    public static SpanBatch CreateGenAiStats(string sessionId, DateTime baseTime) =>
        new(
        [
            SpanBuilder.GenAi("trace-g1", "span-g1")
                .WithName("gpt-call")
                .WithSessionId(sessionId)
                .AtTime(baseTime)
                .WithTokens(100, 50)
                .WithCost(TestConstants.CostLarge)
                .Build(),
            SpanBuilder.GenAi("trace-g2", "span-g2")
                .WithName("gpt-call2")
                .WithSessionId(sessionId)
                .AtTime(baseTime, 110, 90)
                .WithTokens(80, 40)
                .WithCost(TestConstants.CostMedium)
                .Build(),
            SpanBuilder.Create("trace-g3", "span-g3")
                .WithName("non-genai")
                .WithSessionId(sessionId)
                .AtTime(baseTime, 210, 40)
                .WithProvider(null)
                .Build()
        ]);

    /// <summary>Creates a span with large JSON data for stress testing.</summary>
    public static SpanStorageRow CreateLargeDataSpan(string traceId, string spanId,
        int padding = TestConstants.LargeJsonPadding)
    {
        var largeJson = "{\"data\": \"" + new string('X', padding) + "\"}";
        return SpanBuilder.Create(traceId, spanId)
            .WithName(TestConstants.OperationLargeData)
            .WithTiming(TimeProvider.System.GetUtcNow().UtcDateTime, TestConstants.DurationShortMs)
            .WithAttributes(largeJson)
            .Build();
    }
}
