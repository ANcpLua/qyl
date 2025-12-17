using qyl.collector.Storage;

namespace qyl.collector.tests.Helpers;

/// <summary>
///     Builder state for creating SpanStorageRow instances.
///     Uses mutable internal state, then creates immutable SpanStorageRow via Build().
/// </summary>
internal sealed class SpanBuilder
{
    private static int _sCounter;
    private string? _attributes;
    private decimal? _costUsd;
    private DateTime _endTime = DateTime.UtcNow.AddMilliseconds(TestConstants.DurationDefaultMs);
    private string? _evalReason;
    private float? _evalScore;
    private string? _events;
    private string? _kind;
    private string _name = TestConstants.OperationDefault;

    // Optional fields
    private string? _parentSpanId;
    private string? _providerName;
    private string? _requestModel;
    private string? _serviceName;
    private string? _sessionId;
    private string _spanId = "span-000001";
    private DateTime _startTime = DateTime.UtcNow;
    private int? _statusCode;
    private string? _statusMessage;
    private long? _tokensIn;
    private long? _tokensOut;

    // Required fields
    private string _traceId = "trace-000001";

    private SpanBuilder()
    {
    }

    /// <summary>Creates a new SpanBuilder with auto-generated IDs.</summary>
    public static SpanBuilder Create()
    {
        var id = Interlocked.Increment(ref _sCounter);
        var now = DateTime.UtcNow;
        return new SpanBuilder
        {
            _traceId = $"trace-{id:D6}",
            _spanId = $"span-{id:D6}",
            _name = $"operation-{id}",
            _startTime = now,
            _endTime = now.AddMilliseconds(TestConstants.DurationDefaultMs)
        };
    }

    /// <summary>Creates a SpanBuilder with explicit trace and span IDs.</summary>
    public static SpanBuilder Create(string traceId, string spanId)
    {
        var now = DateTime.UtcNow;
        return new SpanBuilder
        {
            _traceId = traceId,
            _spanId = spanId,
            _name = TestConstants.OperationDefault,
            _startTime = now,
            _endTime = now.AddMilliseconds(TestConstants.DurationDefaultMs)
        };
    }

    /// <summary>Creates a minimal span with only required fields.</summary>
    public static SpanBuilder Minimal(string traceId, string spanId)
    {
        var now = DateTime.UtcNow;
        return new SpanBuilder
        {
            _traceId = traceId,
            _spanId = spanId,
            _name = TestConstants.OperationMinimal,
            _startTime = now,
            _endTime = now.AddMilliseconds(TestConstants.DurationShortMs)
        };
    }

    /// <summary>Creates a GenAI span with provider, model, and token data.</summary>
    public static SpanBuilder GenAi(string traceId, string spanId)
    {
        return Create(traceId, spanId)
            .WithProvider(TestConstants.ProviderOpenAi)
            .WithModel(TestConstants.ModelGpt4)
            .WithTokens(TestConstants.TokensInDefault, TestConstants.TokensOutDefault)
            .WithCost(TestConstants.CostDefault);
    }

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

    // Timing
    public SpanBuilder WithTiming(DateTime startTime, double durationMs)
    {
        _startTime = startTime;
        _endTime = startTime.AddMilliseconds(durationMs);
        return this;
    }

    public SpanBuilder WithStartTime(DateTime startTime)
    {
        _startTime = startTime;
        return this;
    }

    public SpanBuilder WithEndTime(DateTime endTime)
    {
        _endTime = endTime;
        return this;
    }

    public SpanBuilder AtTime(DateTime baseTime, int offsetMs = 0, double durationMs = TestConstants.DurationDefaultMs)
    {
        var start = baseTime.AddMilliseconds(offsetMs);
        _startTime = start;
        _endTime = start.AddMilliseconds(durationMs);
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
    public SpanBuilder WithKind(string? kind)
    {
        _kind = kind;
        return this;
    }

    public SpanBuilder WithStatusCode(int? statusCode)
    {
        _statusCode = statusCode;
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
        _providerName = provider;
        return this;
    }

    public SpanBuilder WithModel(string? model)
    {
        _requestModel = model;
        return this;
    }

    public SpanBuilder WithTokens(long? input, long? output)
    {
        _tokensIn = input;
        _tokensOut = output;
        return this;
    }

    public SpanBuilder WithCost(decimal? cost)
    {
        _costUsd = cost;
        return this;
    }

    public SpanBuilder WithEval(float? score, string? reason = null)
    {
        _evalScore = score;
        _evalReason = reason;
        return this;
    }

    // Data
    public SpanBuilder WithAttributes(string? attributes)
    {
        _attributes = attributes;
        return this;
    }

    public SpanBuilder WithEvents(string? events)
    {
        _events = events;
        return this;
    }

    /// <summary>Builds the SpanStorageRow using object initializer.</summary>
    public SpanStorageRow Build()
    {
        return new SpanStorageRow
        {
            TraceId = _traceId,
            SpanId = _spanId,
            ParentSpanId = _parentSpanId,
            Name = _name,
            Kind = _kind,
            StartTime = _startTime,
            EndTime = _endTime,
            StatusCode = _statusCode,
            StatusMessage = _statusMessage,
            ServiceName = _serviceName,
            SessionId = _sessionId,
            ProviderName = _providerName,
            RequestModel = _requestModel,
            TokensIn = _tokensIn,
            TokensOut = _tokensOut,
            CostUsd = _costUsd,
            EvalScore = _evalScore,
            EvalReason = _evalReason,
            Attributes = _attributes,
            Events = _events
        };
    }

    /// <summary>Builds and wraps in a SpanBatch.</summary>
    public SpanBatch ToBatch()
    {
        return new SpanBatch([Build()]);
    }

    /// <summary>Implicit conversion to SpanStorageRow.</summary>
    public static implicit operator SpanStorageRow(SpanBuilder builder)
    {
        return builder.Build();
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
                .AtTime(oldTime, 0, TestConstants.DurationShortMs)
                .Build(),
            SpanBuilder.Create("trace-archive-new", "span-new")
                .WithName("new")
                .WithSessionId(sessionId)
                .AtTime(now, 0, TestConstants.DurationShortMs)
                .Build()
        ]);
    }

    /// <summary>Creates GenAI spans for stats testing.</summary>
    public static SpanBatch CreateGenAiStats(string sessionId, DateTime baseTime)
    {
        return new SpanBatch(
        [
            SpanBuilder.GenAi("trace-g1", "span-g1")
                .WithName("gpt-call")
                .WithSessionId(sessionId)
                .AtTime(baseTime)
                .WithTokens(100, 50)
                .WithCost(TestConstants.CostLarge)
                .WithEval(TestConstants.EvalScoreHigh)
                .Build(),
            SpanBuilder.GenAi("trace-g2", "span-g2")
                .WithName("gpt-call2")
                .WithSessionId(sessionId)
                .AtTime(baseTime, 110, 90)
                .WithTokens(80, 40)
                .WithCost(TestConstants.CostMedium)
                .WithEval(TestConstants.EvalScoreMedium)
                .Build(),
            SpanBuilder.Create("trace-g3", "span-g3")
                .WithName("non-genai")
                .WithSessionId(sessionId)
                .AtTime(baseTime, 210, 40)
                .WithProvider(null)
                .Build()
        ]);
    }

    /// <summary>Creates a span with large JSON data for stress testing.</summary>
    public static SpanStorageRow CreateLargeDataSpan(string traceId, string spanId,
        int padding = TestConstants.LargeJsonPadding)
    {
        var largeJson = "{\"data\": \"" + new string('X', padding) + "\"}";
        return SpanBuilder.Create(traceId, spanId)
            .WithName(TestConstants.OperationLargeData)
            .WithTiming(DateTime.UtcNow, TestConstants.DurationShortMs)
            .WithAttributes(largeJson)
            .WithEvents(largeJson)
            .Build();
    }
}