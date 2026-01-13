// =============================================================================
// qyl DuckDB Schema - Promoted Fields + MAP + Large Content Strategy
// Target: .NET 10 / C# 14 | DuckDB.NET 1.4.3 | OTel SemConv 1.38.0
// =============================================================================

namespace qyl.collector.Storage;

// =============================================================================
// PART 1: DuckDB Schema DDL - Promoted Fields + MAP(VARCHAR, VARCHAR)
// =============================================================================

public static partial class DuckDbSchema
{
    /// <summary>
    /// Schema version. Increment when schema changes require migration.
    /// v2.1.0: Changed timestamp columns from BIGINT to UBIGINT (OTel fixed64 compliance).
    /// </summary>
    public const string Version = "2.1.0";

    /// <summary>
    ///     Core spans table with promoted OTel GenAI fields for columnar performance.
    ///     Non-promoted attributes stored in MAP for flexibility.
    /// </summary>
    public const string CreateSpansTable = """
                                           CREATE TABLE IF NOT EXISTS spans (
                                               -- Identity (Primary Key)
                                               trace_id             VARCHAR(32) NOT NULL,
                                               span_id              VARCHAR(16) NOT NULL,
                                               parent_span_id       VARCHAR(16),

                                               -- Temporal (Partitioning Key) - OTel fixed64 = unsigned 64-bit
                                               start_time_unix_nano UBIGINT NOT NULL,
                                               end_time_unix_nano   UBIGINT NOT NULL,
                                               duration_ns          BIGINT GENERATED ALWAYS AS (CAST(end_time_unix_nano AS BIGINT) - CAST(start_time_unix_nano AS BIGINT)),

                                               -- Core Span Fields
                                               name                 VARCHAR NOT NULL,
                                               kind                 UTINYINT NOT NULL DEFAULT 0,  -- SpanKind enum
                                               status_code          UTINYINT NOT NULL DEFAULT 0,  -- StatusCode enum
                                               status_message       VARCHAR,

                                               -- Resource Attributes (Promoted)
                                               "service.name"       VARCHAR,
                                               "service.version"    VARCHAR,
                                               "service.namespace"  VARCHAR,
                                               "deployment.environment" VARCHAR,

                                               -- GenAI Attributes (Promoted) - OTel 1.38
                                               "gen_ai.provider.name"      VARCHAR,
                                               "gen_ai.request.model"      VARCHAR,
                                               "gen_ai.response.model"     VARCHAR,
                                               "gen_ai.operation.name"     VARCHAR,
                                               "gen_ai.usage.input_tokens" BIGINT,
                                               "gen_ai.usage.output_tokens" BIGINT,
                                               "gen_ai.request.temperature" DOUBLE,
                                               "gen_ai.request.max_tokens" BIGINT,
                                               "gen_ai.request.top_p"      DOUBLE,
                                               "gen_ai.response.id"        VARCHAR,
                                               "gen_ai.response.finish_reasons" VARCHAR[],

                                               -- Agent & Tool Attributes (OTel 1.38 gen_ai.agent.* / gen_ai.tool.*)
                                               "gen_ai.agent.id"           VARCHAR,
                                               "gen_ai.agent.name"         VARCHAR,
                                               "gen_ai.tool.name"          VARCHAR,
                                               "gen_ai.tool.call.id"       VARCHAR,
                                               "gen_ai.tool.type"          VARCHAR,
                                               "gen_ai.conversation.id"    VARCHAR,

                                               -- Session/Request Tracking (Promoted)
                                               "session.id"                VARCHAR,
                                               "user.id"                   VARCHAR,
                                               "http.request.id"           VARCHAR,

                                               -- Error Tracking (Promoted)
                                               "exception.type"            VARCHAR,
                                               "exception.message"         VARCHAR,

                                               -- Large Content References (External Storage)
                                               "gen_ai.prompt.ref"         VARCHAR,  -- Reference to content store
                                               "gen_ai.completion.ref"     VARCHAR,

                                               -- Non-Promoted Attributes (MAP for flexibility)
                                               attributes MAP(VARCHAR, VARCHAR) NOT NULL DEFAULT MAP {},

                                               -- Events & Links (Stored as JSON arrays)
                                               events VARCHAR,  -- JSON array of SpanEvent
                                               links  VARCHAR,  -- JSON array of SpanLink

                                               -- Metadata
                                               ingested_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,

                                               PRIMARY KEY (trace_id, span_id)
                                           );
                                           """;

    /// <summary>
    ///     Large content storage for gen_ai.prompt/completion >4KB.
    ///     Uses ZSTD compression for efficient storage.
    /// </summary>
    public const string CreateContentTable = """
                                             CREATE TABLE IF NOT EXISTS span_content (
                                                 content_id   VARCHAR(64) NOT NULL PRIMARY KEY,  -- SHA256 hash
                                                 trace_id     VARCHAR(32) NOT NULL,
                                                 span_id      VARCHAR(16) NOT NULL,
                                                 content_type VARCHAR(32) NOT NULL,  -- 'gen_ai.prompt', 'gen_ai.completion'
                                                 content_raw  BLOB NOT NULL,          -- ZSTD compressed JSON
                                                 size_bytes   BIGINT NOT NULL,
                                                 compressed_bytes BIGINT NOT NULL,
                                                 created_at   TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,

                                                 FOREIGN KEY (trace_id, span_id) REFERENCES spans(trace_id, span_id)
                                             );
                                             """;

    /// <summary>
    ///     Session aggregation table for real-time analytics.
    /// </summary>
    public const string CreateSessionsTable = """
                                              CREATE TABLE IF NOT EXISTS sessions (
                                                  session_id           VARCHAR NOT NULL PRIMARY KEY,
                                                  first_span_time      UBIGINT NOT NULL,  -- OTel fixed64 = unsigned
                                                  last_span_time       UBIGINT NOT NULL,  -- OTel fixed64 = unsigned
                                                  span_count           BIGINT NOT NULL DEFAULT 0,
                                                  total_input_tokens   BIGINT NOT NULL DEFAULT 0,
                                                  total_output_tokens  BIGINT NOT NULL DEFAULT 0,
                                                  total_duration_ns    BIGINT NOT NULL DEFAULT 0,
                                                  error_count          BIGINT NOT NULL DEFAULT 0,

                                                  -- Aggregated model usage
                                                  models_used          VARCHAR[],
                                                  providers_used       VARCHAR[],
                                                  operations_used      VARCHAR[],

                                                  -- User context
                                                  user_id              VARCHAR,

                                                  -- Metadata
                                                  updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
                                              );
                                              """;

    /// <summary>
    ///     Optimized indexes for common query patterns.
    /// </summary>
    public const string CreateIndexes = """
                                        -- Temporal queries (most common)
                                        CREATE INDEX IF NOT EXISTS idx_spans_time
                                            ON spans (start_time_unix_nano DESC);

                                        -- Session-based queries
                                        CREATE INDEX IF NOT EXISTS idx_spans_session
                                            ON spans ("session.id")
                                            WHERE "session.id" IS NOT NULL;

                                        -- GenAI analytics
                                        CREATE INDEX IF NOT EXISTS idx_spans_genai_provider
                                            ON spans ("gen_ai.provider.name", "gen_ai.request.model")
                                            WHERE "gen_ai.provider.name" IS NOT NULL;

                                        -- Error analysis
                                        CREATE INDEX IF NOT EXISTS idx_spans_errors
                                            ON spans (status_code, "exception.type")
                                            WHERE status_code = 2;

                                        -- Trace reconstruction
                                        CREATE INDEX IF NOT EXISTS idx_spans_trace
                                            ON spans (trace_id, start_time_unix_nano);

                                        -- Content lookup
                                        CREATE INDEX IF NOT EXISTS idx_content_span
                                            ON span_content (trace_id, span_id);
                                        """;

    /// <summary>
    ///     Materialized view for DORA metrics calculation.
    /// </summary>
    public const string CreateDoraMetricsView = """
                                                CREATE OR REPLACE VIEW dora_metrics AS
                                                WITH deployments AS (
                                                    SELECT
                                                        DATE_TRUNC('day', TO_TIMESTAMP(start_time_unix_nano / 1000000000)) AS day,
                                                        "deployment.environment" AS environment,
                                                        COUNT(DISTINCT trace_id) AS deployment_count
                                                    FROM spans
                                                    WHERE "gen_ai.operation.name" = 'deploy'
                                                    GROUP BY 1, 2
                                                ),
                                                failures AS (
                                                    SELECT
                                                        DATE_TRUNC('day', TO_TIMESTAMP(start_time_unix_nano / 1000000000)) AS day,
                                                        "deployment.environment" AS environment,
                                                        COUNT(*) AS failure_count
                                                    FROM spans
                                                    WHERE status_code = 2
                                                    GROUP BY 1, 2
                                                )
                                                SELECT
                                                    COALESCE(d.day, f.day) AS day,
                                                    COALESCE(d.environment, f.environment) AS environment,
                                                    COALESCE(d.deployment_count, 0) AS deployment_frequency,
                                                    COALESCE(f.failure_count, 0) AS change_failure_count,
                                                    CASE
                                                        WHEN d.deployment_count > 0
                                                        THEN ROUND(f.failure_count::DOUBLE / d.deployment_count * 100, 2)
                                                        ELSE 0
                                                    END AS change_failure_rate
                                                FROM deployments d
                                                FULL OUTER JOIN failures f ON d.day = f.day AND d.environment = f.environment;
                                                """;

    /// <summary>
    ///     Top models analytics view.
    /// </summary>
    public const string CreateTopModelsView = """
                                              CREATE OR REPLACE VIEW top_models AS
                                              SELECT
                                                  "gen_ai.provider.name" AS provider,
                                                  "gen_ai.request.model" AS model,
                                                  COUNT(*) AS call_count,
                                                  SUM("gen_ai.usage.input_tokens") AS total_input_tokens,
                                                  SUM("gen_ai.usage.output_tokens") AS total_output_tokens,
                                                  AVG(duration_ns / 1000000.0) AS avg_latency_ms,
                                                  PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY duration_ns / 1000000.0) AS p95_latency_ms,
                                                  PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY duration_ns / 1000000.0) AS p99_latency_ms,
                                                  SUM(CASE WHEN status_code = 2 THEN 1 ELSE 0 END)::DOUBLE / COUNT(*) * 100 AS error_rate
                                              FROM spans
                                              WHERE "gen_ai.provider.name" IS NOT NULL
                                              GROUP BY 1, 2
                                              ORDER BY call_count DESC;
                                              """;

    public static async Task InitializeAsync(DuckDBConnection connection)
    {
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = CreateSpansTable;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = CreateContentTable;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = CreateSessionsTable;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = CreateIndexes;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = CreateDoraMetricsView;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = CreateTopModelsView;
        await cmd.ExecuteNonQueryAsync();
    }
}

// =============================================================================
// PART 2: Promoted Fields Registry - Source of Truth
// =============================================================================

/// <summary>
///     Canonical registry of promoted fields. Used by:
///     - DuckDB schema generation
///     - Span insertion logic
///     - Roslyn analyzer (QYL003)
///     - TypeSpec schema validation
/// </summary>
public static class PromotedFields
{
    /// <summary>
    ///     All promoted attribute keys with their DuckDB column types.
    /// </summary>
    public static readonly FrozenDictionary<string, ColumnDef> All = new Dictionary<string, ColumnDef>
    {
        // Resource
        ["service.name"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["service.version"] = new("VARCHAR", PromotionReason.Filtering),
        ["service.namespace"] = new("VARCHAR", PromotionReason.Filtering),
        ["deployment.environment"] = new("VARCHAR", PromotionReason.Partitioning),

        // GenAI Core (OTel 1.38 Required)
        ["gen_ai.provider.name"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["gen_ai.request.model"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["gen_ai.response.model"] = new("VARCHAR", PromotionReason.Analytics),
        ["gen_ai.operation.name"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["gen_ai.usage.input_tokens"] = new("BIGINT", PromotionReason.Aggregation),
        ["gen_ai.usage.output_tokens"] = new("BIGINT", PromotionReason.Aggregation),
        ["gen_ai.request.temperature"] = new("DOUBLE", PromotionReason.Analytics),
        ["gen_ai.request.max_output_tokens"] = new("BIGINT", PromotionReason.Analytics),
        ["gen_ai.request.top_p"] = new("DOUBLE", PromotionReason.Analytics),
        ["gen_ai.response.id"] = new("VARCHAR", PromotionReason.Correlation),
        ["gen_ai.response.finish_reasons"] = new("VARCHAR[]", PromotionReason.Analytics),

        // Agent & Tool (OTel 1.38 gen_ai.agent.* / gen_ai.tool.*)
        ["gen_ai.agent.id"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["gen_ai.agent.name"] = new("VARCHAR", PromotionReason.Filtering),
        ["gen_ai.tool.name"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["gen_ai.tool.call.id"] = new("VARCHAR", PromotionReason.Correlation),
        ["gen_ai.tool.type"] = new("VARCHAR", PromotionReason.Filtering),
        ["gen_ai.conversation.id"] = new("VARCHAR", PromotionReason.HighCardinality),

        // Session/User
        ["session.id"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["user.id"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["http.request.id"] = new("VARCHAR", PromotionReason.Correlation),

        // Errors
        ["exception.type"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["exception.message"] = new("VARCHAR", PromotionReason.Filtering)
    }.ToFrozenDictionary();

    /// <summary>
    ///     Large content attributes that should be externalized to span_content table.
    ///     OTel 1.38: gen_ai.input.messages, gen_ai.output.messages, gen_ai.system_instructions
    /// </summary>
    public static readonly FrozenSet<string> LargeContentAttributes = new[]
    {
        "gen_ai.input.messages", "gen_ai.output.messages", "gen_ai.system_instructions", "gen_ai.tool.definitions",
        // Legacy names (deprecated but still need to handle)
        "gen_ai.prompt", "gen_ai.completion"
    }.ToFrozenSet();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPromoted(string key) => All.ContainsKey(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLargeContent(string key) => LargeContentAttributes.Contains(key);

    /// <summary>
    ///     Delegates to SchemaNormalizer.TryGetCurrentName (single source of truth).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCurrentName(string key, [NotNullWhen(true)] out string? current) =>
        SchemaNormalizer.TryGetCurrentName(key, out current);

    public readonly record struct ColumnDef(string DuckDbType, PromotionReason Reason);
}

public enum PromotionReason
{
    HighCardinality, // Frequently used in WHERE/GROUP BY
    Aggregation, // Used in SUM/AVG/COUNT
    Filtering, // Used in WHERE predicates
    Partitioning, // Time-series partitioning key
    Correlation, // Join/correlation key
    Analytics // Dashboard visualization
}

// =============================================================================
// PART 3: Large Content Handler (ZSTD Compression + External Storage)
// =============================================================================

/// <summary>
///     Handles >4KB content (gen_ai.prompt, gen_ai.completion) with ZSTD compression.
///     Content is stored externally and referenced by SHA256 hash.
/// </summary>
public sealed class LargeContentHandler(DuckDBConnection connection)
{
    private const int ThresholdBytes = 4096;
    private static readonly ArrayPool<byte> SharedPool = ArrayPool<byte>.Shared;

    /// <summary>
    ///     Process attribute value, externalizing if too large.
    ///     Returns content_id reference or null if inline.
    /// </summary>
    public async ValueTask<string?> ProcessAttributeAsync(
        TraceId traceId,
        SpanId spanId,
        string attributeKey,
        string value,
        CancellationToken ct = default)
    {
        if (!PromotedFields.IsLargeContent(attributeKey))
            return null;

        var valueBytes = Encoding.UTF8.GetBytes(value);
        if (valueBytes.Length <= ThresholdBytes)
            return null;

        // Compress with ZSTD
        var compressed = CompressZstd(valueBytes);
        var contentId = ComputeContentId(valueBytes);

        // Store in span_content table
        await StoreContentAsync(
            contentId,
            traceId,
            spanId,
            attributeKey,
            compressed,
            valueBytes.Length,
            ct).ConfigureAwait(false);

        return contentId;
    }

    /// <summary>
    ///     Retrieve and decompress large content.
    /// </summary>
    public async ValueTask<string?> RetrieveContentAsync(
        string contentId,
        CancellationToken ct = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT content_raw, size_bytes
                          FROM span_content
                          WHERE content_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter { Value = contentId });

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            return null;

        var compressed = (byte[])reader.GetValue(0);
        var originalSize = reader.Col(1).GetInt64(0);

        return DecompressZstd(compressed, (int)originalSize);
    }

    /// <summary>
    ///     Batch retrieve content for multiple spans.
    /// </summary>
    public async IAsyncEnumerable<(string ContentId, string Content)> RetrieveContentsAsync(
        IEnumerable<string> contentIds,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var idList = string.Join(',', contentIds.Select(id => $"'{id}'"));

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT content_id, content_raw, size_bytes
                           FROM span_content
                           WHERE content_id IN ({idList})
                           """;

        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var contentId = reader.Col(0).GetString("");
            var compressed = (byte[])reader.GetValue(1);
            var originalSize = reader.Col(2).GetInt64(0);
            var content = DecompressZstd(compressed, (int)originalSize);

            yield return (contentId, content);
        }
    }

    private async ValueTask StoreContentAsync(
        string contentId,
        TraceId traceId,
        SpanId spanId,
        string contentType,
        byte[] compressed,
        int originalSize,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO span_content
                              (content_id, trace_id, span_id, content_type, content_raw, size_bytes, compressed_bytes)
                          VALUES ($1, $2, $3, $4, $5, $6, $7)
                          ON CONFLICT (content_id) DO NOTHING
                          """;

        cmd.Parameters.Add(new DuckDBParameter { Value = contentId });
        cmd.Parameters.Add(new DuckDBParameter { Value = traceId.ToString() });
        cmd.Parameters.Add(new DuckDBParameter { Value = spanId.ToString() });
        cmd.Parameters.Add(new DuckDBParameter { Value = contentType });
        cmd.Parameters.Add(new DuckDBParameter { Value = compressed });
        cmd.Parameters.Add(new DuckDBParameter { Value = (long)originalSize });
        cmd.Parameters.Add(new DuckDBParameter { Value = (long)compressed.Length });

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string ComputeContentId(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(content, hash);
        return Convert.ToHexStringLower(hash);
    }

    private static byte[] CompressZstd(ReadOnlySpan<byte> data)
    {
        using var output = new MemoryStream();
        using (var zstd = new ZLibStream(output, CompressionLevel.Optimal))
        {
            zstd.Write(data);
        }

        return output.ToArray();
    }

    private static string DecompressZstd(byte[] compressed, int originalSize)
    {
        var buffer = SharedPool.Rent(originalSize);
        try
        {
            using var input = new MemoryStream(compressed);
            using var zstd = new ZLibStream(input, CompressionMode.Decompress);
            var bytesRead = zstd.Read(buffer, 0, originalSize);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
        finally
        {
            SharedPool.Return(buffer);
        }
    }
}

// =============================================================================
// PART 4: Span Storage with Attribute Partitioning
// =============================================================================

/// <summary>
///     High-performance span storage with automatic attribute partitioning.
///     Promoted fields → columns, others → MAP, large content → external.
/// </summary>
public sealed class SpanStore(DuckDBConnection connection, LargeContentHandler contentHandler)
{
    /// <summary>
    ///     Insert parsed span with automatic attribute routing.
    /// </summary>
    [RequiresUnreferencedCode("Serializes unknown span attribute types to JSON")]
    [RequiresDynamicCode("Serializes unknown span attribute types to JSON")]
    public async ValueTask InsertSpanAsync(ParsedSpan span, CancellationToken ct = default)
    {
        // Partition attributes
        var (promoted, mapped) = PartitionAttributes(span);

        // Handle large content
        var promptRef = span.Attributes?.FirstOrDefault(a => a.Key == "gen_ai.prompt").Value as string;
        var completionRef = span.Attributes?.FirstOrDefault(a => a.Key == "gen_ai.completion").Value as string;

        if (promptRef is not null && promptRef.Length > 4096)
        {
            var contentId = await contentHandler.ProcessAttributeAsync(
                span.TraceId, span.SpanId, "gen_ai.prompt", promptRef, ct).ConfigureAwait(false);
            if (contentId is not null)
                promoted["gen_ai.prompt.ref"] = contentId;
        }

        if (completionRef is not null && completionRef.Length > 4096)
        {
            var contentId = await contentHandler.ProcessAttributeAsync(
                span.TraceId, span.SpanId, "gen_ai.completion", completionRef, ct).ConfigureAwait(false);
            if (contentId is not null)
                promoted["gen_ai.completion.ref"] = contentId;
        }

        await InsertWithPromotedFieldsAsync(span, promoted, mapped, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Batch insert for high-throughput ingestion.
    /// </summary>
    [RequiresUnreferencedCode("Serializes unknown span attribute types to JSON")]
    [RequiresDynamicCode("Serializes unknown span attribute types to JSON")]
    public ValueTask InsertBatchAsync(
        IReadOnlyList<ParsedSpan> spans,
        CancellationToken ct = default)
    {
        if (spans.Count is 0) return ValueTask.CompletedTask;
        ct.ThrowIfCancellationRequested();

        using var appender = connection.CreateAppender("spans");

        foreach (var span in spans)
        {
            ct.ThrowIfCancellationRequested();
            var (promoted, mapped) = PartitionAttributes(span);

            var row = appender.CreateRow();

            // Identity
            row.AppendValue(span.TraceId.ToString());
            row.AppendValue(span.SpanId.ToString());
            if (span.ParentSpanId.IsEmpty)
                row.AppendNullValue();
            else
                row.AppendValue(span.ParentSpanId.ToString());

            // Temporal
            row.AppendValue(span.StartTime.Value);
            row.AppendValue(span.EndTime.Value);

            // Core
            row.AppendValue(span.Name);
            row.AppendValue((byte)span.Kind);
            row.AppendValue((byte)span.Status);
            if (span.StatusMessage is null)
                row.AppendNullValue();
            else
                row.AppendValue(span.StatusMessage);

            // Promoted fields (in schema order)
            AppendPromotedFields(row, promoted);

            // MAP for non-promoted
            row.AppendValue(SerializeMap(mapped));

            // Events & Links (null for now)
            row.AppendNullValue(); // events JSON
            row.AppendNullValue(); // links JSON

            row.EndRow();
        }

        appender.Close();
        return ValueTask.CompletedTask;
    }

    // Serializes unknown attribute types to JSON - AOT can't statically analyze arbitrary user attributes
    [RequiresUnreferencedCode("Serializes unknown span attribute types to JSON")]
    [RequiresDynamicCode("Serializes unknown span attribute types to JSON")]
    private static (Dictionary<string, object?> Promoted, Dictionary<string, string> Mapped)
        PartitionAttributes(ParsedSpan span)
    {
        var promoted = new Dictionary<string, object?>();
        var mapped = new Dictionary<string, string>();

        // Extract from strongly-typed fields first
        if (span.ProviderName is not null)
            promoted["gen_ai.provider.name"] = span.ProviderName;
        if (span.RequestModel is not null)
            promoted["gen_ai.request.model"] = span.RequestModel;
        if (span.ResponseModel is not null)
            promoted["gen_ai.response.model"] = span.ResponseModel;
        if (span.OperationName is not null)
            promoted["gen_ai.operation.name"] = span.OperationName;
        if (span.InputTokens > 0)
            promoted["gen_ai.usage.input_tokens"] = span.InputTokens;
        if (span.OutputTokens > 0)
            promoted["gen_ai.usage.output_tokens"] = span.OutputTokens;
        if (span.Temperature.HasValue)
            promoted["gen_ai.request.temperature"] = span.Temperature.Value;
        if (span.MaxTokens.HasValue)
            promoted["gen_ai.request.max_tokens"] = span.MaxTokens.Value;
        if (span.SessionId.HasValue)
            promoted["session.id"] = span.SessionId.Value.Value;

        // Process remaining attributes
        if (span.Attributes is null) return (promoted, mapped);
        foreach (var (key, value) in span.Attributes)
        {
            // Handle deprecated mappings
            var effectiveKey = PromotedFields.TryGetCurrentName(key, out var current)
                ? current
                : key;

            if (PromotedFields.IsPromoted(effectiveKey))
                promoted[effectiveKey] = value;
            else if (!PromotedFields.IsLargeContent(effectiveKey))
                // Convert to string for MAP storage
            {
                mapped[effectiveKey] = value switch
                {
                    string s => s,
                    long l => l.ToString(),
                    double d => d.ToString("G17"),
                    bool b => b ? "true" : "false",
                    _ => JsonSerializer.Serialize(value, QylSerializerContext.Default.Options)
                };
            }
        }

        return (promoted, mapped);
    }

    private async ValueTask InsertWithPromotedFieldsAsync(
        ParsedSpan span,
        Dictionary<string, object?> promoted,
        Dictionary<string, string> mapped,
        CancellationToken ct)
    {
        var columns = new List<string>
        {
            "trace_id",
            "span_id",
            "parent_span_id",
            "start_time_unix_nano",
            "end_time_unix_nano",
            "name",
            "kind",
            "status_code",
            "status_message"
        };

        var values = new List<string>
        {
            "$1",
            "$2",
            "$3",
            "$4",
            "$5",
            "$6",
            "$7",
            "$8",
            "$9"
        };

        var paramIndex = 10;
        foreach (var (key, _) in promoted)
        {
            columns.Add($"\"{key}\"");
            values.Add($"${paramIndex++}");
        }

        columns.Add("attributes");
        values.Add($"${paramIndex}");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
                           INSERT INTO spans ({string.Join(", ", columns)})
                           VALUES ({string.Join(", ", values)})
                           """;

        // Core parameters
        cmd.Parameters.Add(new DuckDBParameter { Value = span.TraceId.ToString() });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.SpanId.ToString() });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.ParentSpanId.IsEmpty ? DBNull.Value : span.ParentSpanId.ToString()
        });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.StartTime.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.EndTime.Value });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.Name });
        cmd.Parameters.Add(new DuckDBParameter { Value = (byte)span.Kind });
        cmd.Parameters.Add(new DuckDBParameter { Value = (byte)span.Status });
        cmd.Parameters.Add(new DuckDBParameter { Value = span.StatusMessage ?? (object)DBNull.Value });

        // Promoted field parameters
        foreach (var (_, value) in promoted) cmd.Parameters.Add(new DuckDBParameter { Value = value ?? DBNull.Value });

        // MAP parameter
        cmd.Parameters.Add(new DuckDBParameter { Value = SerializeMap(mapped) });

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string SerializeMap(Dictionary<string, string> map)
    {
        if (map.Count is 0)
            return "MAP {}";

        var pairs = map.Select(kv => $"'{EscapeSql(kv.Key)}': '{EscapeSql(kv.Value)}'");
        return $"MAP {{{string.Join(", ", pairs)}}}";
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

    private static void AppendPromotedFields(IDuckDBAppenderRow row, Dictionary<string, object?> promoted)
    {
        // Must match schema column order exactly (OTel 1.38)
        string[] orderedKeys =
        [
            "service.name",
            "service.version",
            "service.namespace",
            "deployment.environment",
            "gen_ai.provider.name",
            "gen_ai.request.model",
            "gen_ai.response.model",
            "gen_ai.operation.name",
            "gen_ai.usage.input_tokens",
            "gen_ai.usage.output_tokens",
            "gen_ai.request.temperature",
            "gen_ai.request.max_tokens",
            "gen_ai.request.top_p",
            "gen_ai.response.id",
            "gen_ai.response.finish_reasons",
            "gen_ai.agent.id",
            "gen_ai.agent.name",
            "gen_ai.tool.name",
            "gen_ai.tool.call.id",
            "gen_ai.tool.type",
            "gen_ai.conversation.id",
            "session.id",
            "user.id",
            "http.request.id",
            "exception.type",
            "exception.message",
            "gen_ai.prompt.ref",
            "gen_ai.completion.ref"
        ];

        foreach (var key in orderedKeys)
        {
            if (promoted.TryGetValue(key, out var value) && value is not null)
                // Handle different types appropriately
            {
                switch (value)
                {
                    case string s:
                        row.AppendValue(s);
                        break;
                    case long l:
                        row.AppendValue(l);
                        break;
                    case double d:
                        row.AppendValue(d);
                        break;
                    case string[] arr:
                        row.AppendValue(arr);
                        break;
                    default:
                        row.AppendValue(value.ToString() ?? string.Empty);
                        break;
                }
            }
            else
                row.AppendNullValue();
        }
    }
}

// =============================================================================
// PART 7: TypeSpec-Aligned Attribute Value Types
// =============================================================================

/// <summary>
///     OTel-compliant attribute value (homogeneous arrays only).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StringValue), "string")]
[JsonDerivedType(typeof(Int64Value), "int64")]
[JsonDerivedType(typeof(Float64Value), "float64")]
[JsonDerivedType(typeof(BoolValue), "bool")]
[JsonDerivedType(typeof(StringArrayValue), "string_array")]
[JsonDerivedType(typeof(Int64ArrayValue), "int64_array")]
[JsonDerivedType(typeof(Float64ArrayValue), "float64_array")]
[JsonDerivedType(typeof(BoolArrayValue), "bool_array")]
public abstract record OTelAttributeValue
{
    public abstract object? GetValue();

    public static implicit operator OTelAttributeValue(string value) => new StringValue(value);

    public static implicit operator OTelAttributeValue(long value) => new Int64Value(value);

    public static implicit operator OTelAttributeValue(double value) => new Float64Value(value);

    public static implicit operator OTelAttributeValue(bool value) => new BoolValue(value);

    public static implicit operator OTelAttributeValue(string[] value) => new StringArrayValue(value);

    public static implicit operator OTelAttributeValue(long[] value) => new Int64ArrayValue(value);
}

public sealed record StringValue(string Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record Int64Value(long Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record Float64Value(double Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record BoolValue(bool Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record StringArrayValue(string[] Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record Int64ArrayValue(long[] Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record Float64ArrayValue(double[] Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

public sealed record BoolArrayValue(bool[] Value) : OTelAttributeValue
{
    public override object GetValue() => Value;
}

/// <summary>
///     Extended attribute value for GenAI content (allows nested structures).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GenAiMapValue), "map")]
public abstract record GenAiContentValue : OTelAttributeValue;

public sealed record GenAiMapValue(Dictionary<string, OTelAttributeValue> Value) : GenAiContentValue
{
    public override object GetValue() => Value;
}

// =============================================================================
// USAGE EXAMPLES
// =============================================================================
/*
// Initialize DuckDB
await using var connection = new DuckDBConnection("Data Source=qyl.duckdb");
await connection.OpenAsync();
await DuckDbSchema.InitializeAsync(connection);

// Store spans
var contentHandler = new LargeContentHandler(connection);
var store = new SpanStore(connection, contentHandler);
await store.InsertSpanAsync(parsedSpan);

// Top models analytics (uses materialized view)
var topModels = await connection.QueryAsync("""
    SELECT * FROM top_models
    WHERE provider = 'anthropic'
    ORDER BY call_count DESC
    LIMIT 10
    """);

// Retrieve large content
var content = await contentHandler.RetrieveContentAsync(contentId);
*/
