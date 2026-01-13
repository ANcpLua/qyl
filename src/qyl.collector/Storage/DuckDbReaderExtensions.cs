namespace qyl.collector.Storage;

/// <summary>
///     Zero-allocation extensions for IDataReader.
/// </summary>
public static class DuckDbReaderExtensions
{
    extension(IDataReader reader)
    {
        /// <summary>
        ///     Access a column by ordinal with a fluent, zero-allocation accessor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColumnReader Col(int ordinal) => new(reader, ordinal);

        /// <summary>
        ///     Access a column by name.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColumnReader Col(string name) => new(reader, reader.GetOrdinal(name));
    }
}

/// <summary>
///     A zero-allocation ref struct for reading values.
///     Wraps the reader and ordinal to provide a clean, type-safe API.
/// </summary>
[DebuggerDisplay("{ToString(),raw}")]
public readonly ref struct ColumnReader(IDataReader reader, int ordinal)
{
    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => reader.IsDBNull(ordinal);
    }

    // --- Scalars ---

    public string? AsString
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetString(ordinal);
    }

    /// <summary>
    ///     Returns string as ReadOnlySpan&lt;char&gt;.
    ///     Note: Returns ReadOnlySpan.Empty if null to avoid runtime errors.
    /// </summary>
    public ReadOnlySpan<char> Text
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? default : reader.GetString(ordinal).AsSpan();
    }

    public int? AsInt32
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetInt32(ordinal);
    }

    public long? AsInt64
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetInt64(ordinal);
    }

    /// <summary>
    /// Gets UBIGINT (unsigned 64-bit) value. Required for OTel timestamp columns.
    /// DuckDB stores UBIGINT as decimal internally, so we cast via decimal.
    /// </summary>
    public ulong? AsUInt64
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader is DbDataReader db
            ? (ulong)db.GetFieldValue<decimal>(ordinal)
            : (ulong)Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public double? AsDouble
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetDouble(ordinal);
    }

    public decimal? AsDecimal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetDecimal(ordinal);
    }

    /// <summary>DuckDB stores floats as doubles.</summary>
    public float? AsFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : (float)reader.GetDouble(ordinal);
    }

    public DateTime? AsDateTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetDateTime(ordinal);
    }

    // --- Advanced Types (Schema Alignment) ---

    /// <summary>
    ///     Reads a DuckDB LIST/ARRAY column (e.g. VARCHAR[]) as List&lt;T&gt;.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<T>? AsList<T>() => IsNull ? null : reader.GetValue(ordinal) as List<T>;

    /// <summary>
    ///     Reads a DuckDB MAP column as Dictionary&lt;K,V&gt;.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<TKey, TValue>? AsMap<TKey, TValue>() where TKey : notnull =>
        IsNull ? null : reader.GetValue(ordinal) as Dictionary<TKey, TValue>;

    /// <summary>
    ///     Reads a BLOB column as a byte array.
    /// </summary>
    public byte[]? AsBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : reader.GetValue(ordinal) as byte[];
    }

    /// <summary>
    ///     Reads a column as a Stream (efficient for large BLOBs/Strings).
    /// </summary>
    public Stream AsStream
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (IsNull) return Stream.Null;

            // Fast path on ADO.NET readers that support streaming BLOBs efficiently.
            if (reader is DbDataReader db)
                return db.GetStream(ordinal);

            // Fallbacks for providers that materialize BLOBs as byte[].
            var value = reader.GetValue(ordinal);
            return value switch
            {
                Stream s => s,
                byte[] bytes => new MemoryStream(bytes, false),
                _ => throw new InvalidOperationException(
                    $"Column {ordinal} is not a BLOB/Stream (was {value.GetType().FullName}).")
            };
        }
    }

    // --- Fallbacks ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString(string defaultValue) => IsNull ? defaultValue : reader.GetString(ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32(int defaultValue) => IsNull ? defaultValue : reader.GetInt32(ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInt64(long defaultValue) => IsNull ? defaultValue : reader.GetInt64(ordinal);

    /// <summary>
    /// Gets UBIGINT (unsigned 64-bit) value with default. Required for OTel timestamp columns.
    /// DuckDB stores UBIGINT as decimal internally, so we cast via decimal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetUInt64(ulong defaultValue) => IsNull ? defaultValue : reader is DbDataReader db
        ? (ulong)db.GetFieldValue<decimal>(ordinal)
        : (ulong)Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble(double defaultValue) => IsNull ? defaultValue : reader.GetDouble(ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal GetDecimal(decimal defaultValue) => IsNull ? defaultValue : reader.GetDecimal(ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(float defaultValue) => IsNull ? defaultValue : (float)reader.GetDouble(ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime GetDateTime(DateTime defaultValue) => IsNull ? defaultValue : reader.GetDateTime(ordinal);

    public override string ToString()
    {
        if (IsNull) return "NULL";
        try
        {
            return reader.GetValue(ordinal)?.ToString() ?? "NULL";
        }
        catch
        {
            return "Err";
        }
    }
}

public sealed record GenAiStats
{
    public long RequestCount { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public decimal TotalCostUsd { get; init; }
    public float? AverageEvalScore { get; init; }
}

public sealed record SpanBatch(List<SpanStorageRow> Spans);

/// <summary>
///     DuckDB storage row for spans. Uses flat DateTime types for DB compatibility.
///     Owner: qyl.collector | For external API use SpanDto instead.
/// </summary>
public sealed record SpanStorageRow
{
    // Identity
    public required string TraceId { get; init; }
    public required string SpanId { get; init; }
    public string? ParentSpanId { get; init; }

    // Core span fields
    public required string Name { get; init; }
    public string? Kind { get; init; }
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public int? StatusCode { get; init; }
    public string? StatusMessage { get; init; }

    // Resource attributes (OTel: service.name)
    public string? ServiceName { get; init; }

    // Session tracking (OTel: session.id)
    public string? SessionId { get; init; }

    // GenAI attributes (OTel 1.38)
    public string? ProviderName { get; init; } // gen_ai.provider.name
    public string? RequestModel { get; init; } // gen_ai.request.model
    public long? TokensIn { get; init; } // gen_ai.usage.input_tokens (BIGINT)
    public long? TokensOut { get; init; } // gen_ai.usage.output_tokens (BIGINT)

    // qyl extensions
    public decimal? CostUsd { get; init; }
    public float? EvalScore { get; init; }
    public string? EvalReason { get; init; }

    // Flexible storage
    public string? Attributes { get; init; }
    public string? Events { get; init; }
}

public sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
    public long FeedbackCount { get; init; }
    public DateTime? OldestSpan { get; init; }
    public DateTime? NewestSpan { get; init; }
}

public interface ITelemetrySseBroadcaster : IAsyncDisposable
{
    int ClientCount { get; }
    ChannelReader<TelemetryMessage> Subscribe(Guid clientId);
    void Unsubscribe(Guid clientId);
    void Publish(TelemetryMessage item);
    void PublishSpans(SpanBatch batch);
}

public sealed class TelemetrySseBroadcaster : ITelemetrySseBroadcaster
{
    private readonly ConcurrentDictionary<Guid, Channel<TelemetryMessage>> _channels = new();
    private volatile bool _disposed;

    public int ClientCount => _channels.Count;

    public ChannelReader<TelemetryMessage> Subscribe(Guid clientId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateBounded<TelemetryMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = false, SingleReader = true
        });

        _channels[clientId] = channel;
        return channel.Reader;
    }

    public void Unsubscribe(Guid clientId)
    {
        if (_channels.TryRemove(clientId, out var channel))
            channel.Writer.TryComplete();
    }

    public void Publish(TelemetryMessage item)
    {
        if (_disposed) return;

        foreach (var channel in _channels.Values)
            channel.Writer.TryWrite(item);
    }

    public void PublishSpans(SpanBatch batch)
    {
        var message = new TelemetryMessage(TelemetrySignal.Spans, batch, TimeProvider.System.GetUtcNow());
        Publish(message);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;

        foreach (var channel in _channels.Values)
            channel.Writer.TryComplete();

        _channels.Clear();
        return ValueTask.CompletedTask;
    }
}

public enum TelemetrySignal
{
    Connected = 0,
    Spans = 1,
    Metrics = 2,
    Logs = 3,
    Heartbeat = 4
}

public sealed record TelemetryMessage(
    TelemetrySignal Signal,
    object? Data,
    DateTimeOffset Timestamp);

public sealed record TelemetryEventDto(
    string EventType,
    object? Data,
    DateTimeOffset Timestamp);
