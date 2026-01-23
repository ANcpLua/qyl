namespace qyl.collector.Storage;

/// <summary>
///     Zero-allocation extensions for IDataReader.
///     .NET 10 optimized with C# 14 extension members.
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
public readonly ref struct ColumnReader(IDataRecord reader, int ordinal)
{
    private readonly IDataRecord _reader = reader;
    private readonly int _ordinal = ordinal;

    public bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _reader.IsDBNull(_ordinal);
    }

    // --- Scalars ---

    public string? AsString
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetString(_ordinal);
    }

    /// <summary>
    ///     Returns string as ReadOnlySpan&lt;char&gt;.
    ///     Note: Returns ReadOnlySpan.Empty if null to avoid runtime errors.
    /// </summary>
    public ReadOnlySpan<char> Text
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? default : _reader.GetString(_ordinal).AsSpan();
    }

    public int? AsInt32
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetInt32(_ordinal);
    }

    public byte? AsByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetByte(_ordinal);
    }

    public sbyte? AsSByte
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : unchecked((sbyte)_reader.GetByte(_ordinal));
    }

    public long? AsInt64
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetInt64(_ordinal);
    }

    /// <summary>
    ///     Gets UBIGINT (unsigned 64-bit) value. Required for OTel UnixNano columns.
    ///     DuckDB stores UBIGINT as decimal internally, so we cast via decimal.
    /// </summary>
    public ulong? AsUInt64
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull
            ? null
            : _reader is DbDataReader db
                ? (ulong)db.GetFieldValue<decimal>(_ordinal)
                : (ulong)Convert.ToDecimal(_reader.GetValue(_ordinal), CultureInfo.InvariantCulture);
    }

    public double? AsDouble
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetDouble(_ordinal);
    }

    public decimal? AsDecimal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetDecimal(_ordinal);
    }

    /// <summary>DuckDB stores floats as doubles.</summary>
    public float? AsFloat
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : (float)_reader.GetDouble(_ordinal);
    }

    public DateTime? AsDateTime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetDateTime(_ordinal);
    }

    public DateTimeOffset? AsDateTimeOffset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : new DateTimeOffset(_reader.GetDateTime(_ordinal), TimeSpan.Zero);
    }

    // --- Advanced Types (Schema Alignment) ---

    /// <summary>
    ///     Reads a DuckDB LIST/ARRAY column (e.g. VARCHAR[]) as IReadOnlyList&lt;T&gt;.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<T>? AsList<T>() => IsNull ? null : _reader.GetValue(_ordinal) as IReadOnlyList<T>;

    /// <summary>
    ///     Reads a DuckDB MAP column as IReadOnlyDictionary&lt;K,V&gt;.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyDictionary<TKey, TValue>? AsMap<TKey, TValue>() where TKey : notnull =>
        IsNull ? null : _reader.GetValue(_ordinal) as IReadOnlyDictionary<TKey, TValue>;

    /// <summary>
    ///     Reads a BLOB column as a byte array.
    /// </summary>
    public byte[]? AsBytes
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsNull ? null : _reader.GetValue(_ordinal) as byte[];
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
            if (_reader is DbDataReader db)
                return db.GetStream(_ordinal);

            // Fallbacks for providers that materialize BLOBs as byte[].
            var value = _reader.GetValue(_ordinal);
            return value switch
            {
                Stream s => s,
                byte[] bytes => new MemoryStream(bytes, false),
                _ => throw new InvalidOperationException(
                    $"Column {_ordinal} is not a BLOB/Stream (was {value.GetType().FullName}).")
            };
        }
    }

    // --- Fallbacks with defaults ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString(string defaultValue) => IsNull ? defaultValue : _reader.GetString(_ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetInt32(int defaultValue) => IsNull ? defaultValue : _reader.GetInt32(_ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetByte(byte defaultValue) => IsNull ? defaultValue : _reader.GetByte(_ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public sbyte GetSByte(sbyte defaultValue) => IsNull ? defaultValue : unchecked((sbyte)_reader.GetByte(_ordinal));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long GetInt64(long defaultValue) => IsNull ? defaultValue : _reader.GetInt64(_ordinal);

    /// <summary>
    ///     Gets UBIGINT (unsigned 64-bit) value with default. Required for OTel UnixNano columns.
    ///     DuckDB stores UBIGINT as decimal internally, so we cast via decimal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ulong GetUInt64(ulong defaultValue) => IsNull
        ? defaultValue
        : _reader is DbDataReader db
            ? (ulong)db.GetFieldValue<decimal>(_ordinal)
            : (ulong)Convert.ToDecimal(_reader.GetValue(_ordinal), CultureInfo.InvariantCulture);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetDouble(double defaultValue) => IsNull ? defaultValue : _reader.GetDouble(_ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal GetDecimal(decimal defaultValue) => IsNull ? defaultValue : _reader.GetDecimal(_ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetFloat(float defaultValue) => IsNull ? defaultValue : (float)_reader.GetDouble(_ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DateTime GetDateTime(DateTime defaultValue) => IsNull ? defaultValue : _reader.GetDateTime(_ordinal);

    public override string ToString()
    {
        if (IsNull) return "NULL";
        try
        {
            return _reader.GetValue(_ordinal)?.ToString() ?? "NULL";
        }
        catch
        {
            return "Err";
        }
    }
}

// =============================================================================
// Storage Types - Internal to qyl.collector
// =============================================================================

/// <summary>
///     GenAI aggregated statistics for queries.
/// </summary>
public sealed record GenAiStats
{
    public long RequestCount { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
    public double? AverageEvalScore { get; init; }
}

/// <summary>
///     Batch of spans for ingestion. Uses IReadOnlyList for immutable API contract.
/// </summary>
public sealed record SpanBatch(IReadOnlyList<SpanStorageRow> Spans);

/// <summary>
///     DuckDB storage row for spans. Matches generated DuckDbSchema.SpansDdl.
///     Uses UBIGINT timestamps (ulong) for OTel wire format compatibility.
///     Owner: qyl.collector | For external API use SpanRecord from protocol.
/// </summary>
/// <remarks>
///     Generator creates: AddParameters, MapFromReader, BuildMultiRowInsertSql.
///     Property order must match SELECT column order for MapFromReader.
/// </remarks>
[DuckDbTable("spans")]
public sealed partial record SpanStorageRow
{
    // Identity (PRIMARY KEY is span_id in new schema)
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }

    // Core span fields
    public required string Name { get; init; }
    public required byte Kind { get; init; } // TINYINT - SpanKind enum
    [DuckDbColumn(IsUBigInt = true)]
    public required ulong StartTimeUnixNano { get; init; } // UBIGINT
    [DuckDbColumn(IsUBigInt = true)]
    public required ulong EndTimeUnixNano { get; init; } // UBIGINT
    [DuckDbColumn(IsUBigInt = true)]
    public required ulong DurationNs { get; init; } // UBIGINT - computed
    public required byte StatusCode { get; init; } // TINYINT - StatusCode enum
    public string? StatusMessage { get; init; }

    // Resource attributes
    public string? ServiceName { get; init; }

    // GenAI attributes (OTel 1.39 - gen_ai.system is the provider name)
    public string? GenAiSystem { get; init; }
    public string? GenAiRequestModel { get; init; }
    public string? GenAiResponseModel { get; init; }
    public long? GenAiInputTokens { get; init; } // BIGINT
    public long? GenAiOutputTokens { get; init; } // BIGINT
    public double? GenAiTemperature { get; init; } // DOUBLE
    public string? GenAiStopReason { get; init; }
    public string? GenAiToolName { get; init; }
    public string? GenAiToolCallId { get; init; }
    public double? GenAiCostUsd { get; init; } // DOUBLE

    // Flexible storage (JSON columns)
    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }

    // Metadata (auto-generated by DB, exclude from INSERT)
    [DuckDbColumn(ExcludeFromInsert = true)]
    public DateTimeOffset? CreatedAt { get; init; }
}

/// <summary>
///     Storage statistics for monitoring.
/// </summary>
public sealed record StorageStats
{
    public long SpanCount { get; init; }
    public long SessionCount { get; init; }
    public long LogCount { get; init; }
    public ulong? OldestSpanTime { get; init; } // UnixNano
    public ulong? NewestSpanTime { get; init; } // UnixNano
}

/// <summary>
///     DuckDB storage row for logs. Maps to OTLP log records.
///     Owner: qyl.collector | For external API use LogRecord from protocol.
/// </summary>
public sealed record LogStorageRow
{
    public required string LogId { get; init; }
    public string? TraceId { get; init; }
    public string? SpanId { get; init; }
    public string? SessionId { get; init; }

    public required ulong TimeUnixNano { get; init; } // UBIGINT
    public ulong? ObservedTimeUnixNano { get; init; } // UBIGINT

    public required byte SeverityNumber { get; init; } // TINYINT
    public string? SeverityText { get; init; }
    public string? Body { get; init; }

    public string? ServiceName { get; init; }
    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}

// =============================================================================
// SSE Broadcasting - Telemetry streaming infrastructure
// =============================================================================

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

public enum TelemetrySignal : byte
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
