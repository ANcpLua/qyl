// =============================================================================
// qyl DuckDB Schema - Promoted Fields + MAP + Large Content Strategy
// Target: .NET 10 / C# 14 | DuckDB.NET 1.4.3 | OTel SemConv 1.38.0
// =============================================================================

#nullable enable

using System.Buffers;
using System.Collections.Frozen;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckDB.NET.Data;
using qyl.collector.Models;
using qyl.collector.Primitives;

namespace qyl.collector.Storage;

// =============================================================================
// PART 1: DuckDB Schema DDL - Promoted Fields + MAP(VARCHAR, VARCHAR)
// =============================================================================

public static class DuckDbSchema
{
    public const string Version = "2.0.0";

    /// <summary>
    /// Core spans table with promoted OTel GenAI fields for columnar performance.
    /// Non-promoted attributes stored in MAP for flexibility.
    /// </summary>
    public const string CreateSpansTable = """
                                           CREATE TABLE IF NOT EXISTS spans (
                                               -- Identity (Primary Key)
                                               trace_id             VARCHAR(32) NOT NULL,
                                               span_id              VARCHAR(16) NOT NULL,
                                               parent_span_id       VARCHAR(16),

                                               -- Temporal (Partitioning Key)
                                               start_time_unix_nano BIGINT NOT NULL,
                                               end_time_unix_nano   BIGINT NOT NULL,
                                               duration_ns          BIGINT GENERATED ALWAYS AS (end_time_unix_nano - start_time_unix_nano),

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

                                               -- Agent Attributes (Promoted) - anthropic.*/agents.* registry
                                               "agents.agent.id"           VARCHAR,
                                               "agents.agent.name"         VARCHAR,
                                               "agents.tool.name"          VARCHAR,
                                               "agents.tool.call_id"       VARCHAR,

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
    /// Large content storage for gen_ai.prompt/completion >4KB.
    /// Uses ZSTD compression for efficient storage.
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
    /// Session aggregation table for real-time analytics.
    /// </summary>
    public const string CreateSessionsTable = """
                                              CREATE TABLE IF NOT EXISTS sessions (
                                                  session_id           VARCHAR NOT NULL PRIMARY KEY,
                                                  first_span_time      BIGINT NOT NULL,
                                                  last_span_time       BIGINT NOT NULL,
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
    /// Optimized indexes for common query patterns.
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
    /// Materialized view for DORA metrics calculation.
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
    /// Top models analytics view.
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
        await using DuckDBCommand cmd = connection.CreateCommand();

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
/// Canonical registry of promoted fields. Used by:
/// - DuckDB schema generation
/// - Span insertion logic
/// - Roslyn analyzer (QYL003)
/// - TypeSpec schema validation
/// </summary>
public static class PromotedFields
{
    /// <summary>
    /// All promoted attribute keys with their DuckDB column types.
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
        ["gen_ai.request.max_tokens"] = new("BIGINT", PromotionReason.Analytics),
        ["gen_ai.request.top_p"] = new("DOUBLE", PromotionReason.Analytics),
        ["gen_ai.response.id"] = new("VARCHAR", PromotionReason.Correlation),
        ["gen_ai.response.finish_reasons"] = new("VARCHAR[]", PromotionReason.Analytics),

        // Agents (anthropic.*/agents.* registry)
        ["agents.agent.id"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["agents.agent.name"] = new("VARCHAR", PromotionReason.Filtering),
        ["agents.tool.name"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["agents.tool.call_id"] = new("VARCHAR", PromotionReason.Correlation),

        // Session/User
        ["session.id"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["user.id"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["http.request.id"] = new("VARCHAR", PromotionReason.Correlation),

        // Errors
        ["exception.type"] = new("VARCHAR", PromotionReason.HighCardinality),
        ["exception.message"] = new("VARCHAR", PromotionReason.Filtering),
    }.ToFrozenDictionary();

    /// <summary>
    /// Large content attributes that should be externalized to span_content table.
    /// </summary>
    public static readonly FrozenSet<string> LargeContentAttributes = new[]
    {
        "gen_ai.prompt",
        "gen_ai.completion",
        "gen_ai.request.messages",
        "gen_ai.response.choices",
    }.ToFrozenSet();

    /// <summary>
    /// Deprecated attribute mappings (OTel 1.38 migration).
    /// </summary>
    public static readonly FrozenDictionary<string, string> DeprecatedMappings = new Dictionary<string, string>
    {
        ["gen_ai.system"] = "gen_ai.provider.name",
        ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
        ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",
    }.ToFrozenDictionary();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPromoted(string key) => All.ContainsKey(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLargeContent(string key) => LargeContentAttributes.Contains(key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetCurrentName(string key, [NotNullWhen(true)] out string? current)
        => DeprecatedMappings.TryGetValue(key, out current);

    public readonly record struct ColumnDef(string DuckDbType, PromotionReason Reason);
}

public enum PromotionReason
{
    HighCardinality, // Frequently used in WHERE/GROUP BY
    Aggregation, // Used in SUM/AVG/COUNT
    Filtering, // Used in WHERE predicates
    Partitioning, // Time-series partitioning key
    Correlation, // Join/correlation key
    Analytics, // Dashboard visualization
}

// =============================================================================
// PART 3: Large Content Handler (ZSTD Compression + External Storage)
// =============================================================================

/// <summary>
/// Handles >4KB content (gen_ai.prompt, gen_ai.completion) with ZSTD compression.
/// Content is stored externally and referenced by SHA256 hash.
/// </summary>
public sealed class LargeContentHandler(DuckDBConnection connection)
{
    private const int _thresholdBytes = 4096;
    private static readonly ArrayPool<byte> _sPool = ArrayPool<byte>.Shared;

    /// <summary>
    /// Process attribute value, externalizing if too large.
    /// Returns content_id reference or null if inline.
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
        if (valueBytes.Length <= _thresholdBytes)
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
            ct);

        return contentId;
    }

    /// <summary>
    /// Retrieve and decompress large content.
    /// </summary>
    public async ValueTask<string?> RetrieveContentAsync(
        string contentId,
        CancellationToken ct = default)
    {
        await using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
                          SELECT content_raw, size_bytes
                          FROM span_content
                          WHERE content_id = $1
                          """;
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = contentId
        });

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        var compressed = (byte[])reader.GetValue(0);
        var originalSize = reader.GetInt64(1);

        return DecompressZstd(compressed, (int)originalSize);
    }

    /// <summary>
    /// Batch retrieve content for multiple spans.
    /// </summary>
    public async IAsyncEnumerable<(string ContentId, string Content)> RetrieveContentsAsync(
        IEnumerable<string> contentIds,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var idList = string.Join(',', contentIds.Select(id => $"'{id}'"));

        await using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"""
                           SELECT content_id, content_raw, size_bytes
                           FROM span_content
                           WHERE content_id IN ({idList})
                           """;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var contentId = reader.GetString(0);
            var compressed = (byte[])reader.GetValue(1);
            var originalSize = reader.GetInt64(2);
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
        await using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
                          INSERT INTO span_content
                              (content_id, trace_id, span_id, content_type, content_raw, size_bytes, compressed_bytes)
                          VALUES ($1, $2, $3, $4, $5, $6, $7)
                          ON CONFLICT (content_id) DO NOTHING
                          """;

        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = contentId
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = traceId.ToString()
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = spanId.ToString()
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = contentType
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = compressed
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = (long)originalSize
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = (long)compressed.Length
        });

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string ComputeContentId(ReadOnlySpan<byte> content)
    {
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(content, hash);
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
        var buffer = _sPool.Rent(originalSize);
        try
        {
            using var input = new MemoryStream(compressed);
            using var zstd = new ZLibStream(input, CompressionMode.Decompress);
            var bytesRead = zstd.Read(buffer, 0, originalSize);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
        finally
        {
            _sPool.Return(buffer);
        }
    }
}

// =============================================================================
// PART 4: Span Storage with Attribute Partitioning
// =============================================================================

/// <summary>
/// High-performance span storage with automatic attribute partitioning.
/// Promoted fields → columns, others → MAP, large content → external.
/// </summary>
public sealed class SpanStore(DuckDBConnection connection, LargeContentHandler contentHandler)
{
    private readonly Lock _lock = new();

    /// <summary>
    /// Insert parsed span with automatic attribute routing.
    /// </summary>
    public async ValueTask InsertSpanAsync(ParsedSpan span, CancellationToken ct = default)
    {
        // Partition attributes
        (var promoted, var mapped) = PartitionAttributes(span);

        // Handle large content
        var promptRef = span.Attributes?.FirstOrDefault(a => a.Key == "gen_ai.prompt").Value as string;
        var completionRef = span.Attributes?.FirstOrDefault(a => a.Key == "gen_ai.completion").Value as string;

        if (promptRef is not null && promptRef.Length > 4096)
        {
            var contentId = await contentHandler.ProcessAttributeAsync(
                span.TraceId, span.SpanId, "gen_ai.prompt", promptRef, ct);
            if (contentId is not null)
                promoted["gen_ai.prompt.ref"] = contentId;
        }

        if (completionRef is not null && completionRef.Length > 4096)
        {
            var contentId = await contentHandler.ProcessAttributeAsync(
                span.TraceId, span.SpanId, "gen_ai.completion", completionRef, ct);
            if (contentId is not null)
                promoted["gen_ai.completion.ref"] = contentId;
        }

        await InsertWithPromotedFieldsAsync(span, promoted, mapped, ct);
    }

    /// <summary>
    /// Batch insert for high-throughput ingestion.
    /// </summary>
    public async ValueTask InsertBatchAsync(
        IReadOnlyList<ParsedSpan> spans,
        CancellationToken ct = default)
    {
        if (spans.Count == 0) return;

        using DuckDBAppender appender = connection.CreateAppender("spans");

        foreach (var span in spans)
        {
            (var promoted, var mapped) = PartitionAttributes(span);

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
    }

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
        if (span.Attributes is not null)
        {
            foreach ((var key, var value) in span.Attributes)
            {
                // Handle deprecated mappings
                var effectiveKey = PromotedFields.TryGetCurrentName(key, out var current)
                    ? current
                    : key;

                if (PromotedFields.IsPromoted(effectiveKey))
                {
                    promoted[effectiveKey] = value;
                }
                else if (!PromotedFields.IsLargeContent(effectiveKey))
                {
                    // Convert to string for MAP storage
                    mapped[effectiveKey] = value switch
                    {
                        string s => s,
                        long l => l.ToString(),
                        double d => d.ToString("G17"),
                        bool b => b ? "true" : "false",
                        _ => JsonSerializer.Serialize(value)
                    };
                }
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
        foreach ((var key, var _) in promoted)
        {
            columns.Add($"\"{key}\"");
            values.Add($"${paramIndex++}");
        }

        columns.Add("attributes");
        values.Add($"${paramIndex}");

        await using DuckDBCommand cmd = connection.CreateCommand();
        cmd.CommandText = $"""
                           INSERT INTO spans ({string.Join(", ", columns)})
                           VALUES ({string.Join(", ", values)})
                           """;

        // Core parameters
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.TraceId.ToString()
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.SpanId.ToString()
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.ParentSpanId.IsEmpty ? DBNull.Value : span.ParentSpanId.ToString()
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.StartTime.Value
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.EndTime.Value
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.Name
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = (byte)span.Kind
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = (byte)span.Status
        });
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = span.StatusMessage ?? (object)DBNull.Value
        });

        // Promoted field parameters
        foreach ((var _, var value) in promoted)
        {
            cmd.Parameters.Add(new DuckDBParameter
            {
                Value = value ?? DBNull.Value
            });
        }

        // MAP parameter
        cmd.Parameters.Add(new DuckDBParameter
        {
            Value = SerializeMap(mapped)
        });

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string SerializeMap(Dictionary<string, string> map)
    {
        if (map.Count == 0)
            return "MAP {}";

        var pairs = map.Select(kv => $"'{EscapeSql(kv.Key)}': '{EscapeSql(kv.Value)}'");
        return $"MAP {{{string.Join(", ", pairs)}}}";
    }

    private static string EscapeSql(string value)
        => value.Replace("'", "''");

    private static void AppendPromotedFields(IDuckDBAppenderRow row, Dictionary<string, object?> promoted)
    {
        // Must match schema column order exactly
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
            "agents.agent.id",
            "agents.agent.name",
            "agents.tool.name",
            "agents.tool.call_id",
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
            {
                // Handle different types appropriately
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
            {
                row.AppendNullValue();
            }
        }
    }
}

// =============================================================================
// PART 5: Roslyn Analyzer - QYL003: Attribute Should Be Promoted
// =============================================================================
/*
 * Roslyn Analyzer Implementation (separate project: Qyl.Analyzers)
 *
 * File: QYL003AttributeShouldBePromotedAnalyzer.cs
 */

#if ROSLYN_ANALYZER
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Qyl.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QYL003AttributeShouldBePromotedAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QYL003";

    private static readonly LocalizableString Title =
        "Attribute should be promoted";

    private static readonly LocalizableString MessageFormat =
        "Attribute '{0}' is a promoted field and should use the strongly-typed property instead of SetTag/attributes dictionary";

    private static readonly LocalizableString Description =
        "Promoted attributes should be set via strongly-typed properties for type safety and DuckDB columnar performance.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        "Qyl.Performance",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => [Rule];

    // Promoted field keys that should trigger the warning
    private static readonly ImmutableHashSet<string> s_promotedFields =
    [
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
        "session.id",
        "user.id",
        "agents.agent.id",
        "agents.agent.name",
        "agents.tool.name",
        "agents.tool.call_id",
        "exception.type",
        "exception.message",
        "service.name",
        "service.version",
        "deployment.environment",
    ];

    // Deprecated fields that should ALSO trigger (with additional message)
    private static readonly ImmutableDictionary<string, string> s_deprecatedFields =
        new Dictionary<string, string>
        {
            ["gen_ai.system"] = "gen_ai.provider.name",
            ["gen_ai.usage.prompt_tokens"] = "gen_ai.usage.input_tokens",
            ["gen_ai.usage.completion_tokens"] = "gen_ai.usage.output_tokens",
        }.ToImmutableDictionary();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Analyze invocations like activity.SetTag("key", value)
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

        // Analyze dictionary/collection initializers
        context.RegisterSyntaxNodeAction(AnalyzeInitializer, SyntaxKind.ObjectInitializerExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        // Check for SetTag, AddTag, SetAttribute patterns
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName is not ("SetTag" or "AddTag" or "SetAttribute" or "Add"))
            return;

        // Get the first argument (the key)
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 1)
            return;

        var keyArg = arguments[0].Expression;
        if (keyArg is not LiteralExpressionSyntax literal)
            return;

        if (literal.Token.Value is not string key)
            return;

        // Check if it's a promoted field
        if (s_promotedFields.Contains(key))
        {
            var diagnostic = Diagnostic.Create(Rule, literal.GetLocation(), key);
            context.ReportDiagnostic(diagnostic);
        }
        else if (s_deprecatedFields.TryGetValue(key, out var replacement))
        {
            // Create enhanced message for deprecated fields
            var deprecatedRule = new DiagnosticDescriptor(
                "QYL004",
                "Deprecated attribute key",
                $"Attribute '{key}' is deprecated. Use '{replacement}' instead (promoted field)",
                "Qyl.Compatibility",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            var diagnostic = Diagnostic.Create(deprecatedRule, literal.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeInitializer(SyntaxNodeAnalysisContext context)
    {
        var initializer = (InitializerExpressionSyntax)context.Node;

        // Check parent type to see if this is an attributes dictionary
        var parentType = context.SemanticModel.GetTypeInfo(initializer.Parent!).Type;
        if (parentType is null)
            return;

        // Check if it's Dictionary<string, ...> or similar
        if (!parentType.Name.Contains("Dictionary") &&
            !parentType.Name.Contains("KeyValuePair"))
            return;

        foreach (var expression in initializer.Expressions)
        {
            // Handle { "key", value } syntax
            if (expression is InitializerExpressionSyntax kvpInit &&
                kvpInit.Expressions.Count >= 1 &&
                kvpInit.Expressions[0] is LiteralExpressionSyntax keyLiteral &&
                keyLiteral.Token.Value is string key)
            {
                if (s_promotedFields.Contains(key))
                {
                    var diagnostic = Diagnostic.Create(Rule, keyLiteral.GetLocation(), key);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Handle ["key"] = value syntax
            if (expression is AssignmentExpressionSyntax assignment &&
                assignment.Left is ImplicitElementAccessSyntax elementAccess &&
                elementAccess.ArgumentList.Arguments.Count == 1 &&
                elementAccess.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax indexLiteral &&
                indexLiteral.Token.Value is string indexKey)
            {
                if (s_promotedFields.Contains(indexKey))
                {
                    var diagnostic = Diagnostic.Create(Rule, indexLiteral.GetLocation(), indexKey);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}

/// <summary>
/// Code fix provider for QYL003 - suggests using strongly-typed property.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(QYL003CodeFixProvider))]
public sealed class QYL003CodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds
        => ["QYL003", "QYL004"];

    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            if (node is LiteralExpressionSyntax literal &&
                literal.Token.Value is string key)
            {
                var propertyName = GetPropertyName(key);
                if (propertyName is null) continue;

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Use {propertyName} property",
                        createChangedDocument: ct => UsePropertyAsync(
                            context.Document, literal, propertyName, ct),
                        equivalenceKey: $"QYL003_{propertyName}"),
                    diagnostic);
            }
        }
    }

    private static string? GetPropertyName(string attributeKey) => attributeKey switch
    {
        "gen_ai.provider.name" or "gen_ai.system" => "ProviderName",
        "gen_ai.request.model" => "RequestModel",
        "gen_ai.response.model" => "ResponseModel",
        "gen_ai.operation.name" => "OperationName",
        "gen_ai.usage.input_tokens" or "gen_ai.usage.prompt_tokens" => "InputTokens",
        "gen_ai.usage.output_tokens" or "gen_ai.usage.completion_tokens" => "OutputTokens",
        "gen_ai.request.temperature" => "Temperature",
        "gen_ai.request.max_tokens" => "MaxTokens",
        "session.id" => "SessionId",
        _ => null
    };

    private static async Task<Document> UsePropertyAsync(
        Document document,
        LiteralExpressionSyntax literal,
        string propertyName,
        CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        // Find the containing invocation
        var invocation = literal.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (invocation is null) return document;

        // Get the receiver (e.g., 'span' in span.SetTag(...))
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            return document;

        var receiver = memberAccess.Expression;

        // Get the value argument
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2) return document;

        var valueArg = arguments[1].Expression;

        // Create: receiver.PropertyName = value
        var newAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                receiver,
                SyntaxFactory.IdentifierName(propertyName)),
            valueArg);

        var newStatement = SyntaxFactory.ExpressionStatement(newAssignment)
            .WithTriviaFrom(invocation.Parent!);

        var newRoot = root.ReplaceNode(invocation.Parent!, newStatement);
        return document.WithSyntaxRoot(newRoot);
    }
}
#endif

// =============================================================================
// PART 6: Query Builder with Promoted Field Optimization
// =============================================================================

/// <summary>
/// Query builder that automatically uses promoted columns vs MAP access.
/// </summary>
public sealed class SpanQueryBuilder
{
    private readonly List<string> _select = [];
    private readonly List<string> _where = [];
    private readonly List<string> _groupBy = [];
    private readonly List<string> _orderBy = [];
    private readonly Dictionary<string, object> _parameters = [];
    private int _paramIndex = 1;
    private int? _limit;
    private int? _offset;

    public SpanQueryBuilder Select(params string[] columns)
    {
        foreach (var col in columns)
        {
            _select.Add(GetColumnAccess(col));
        }

        return this;
    }

    public SpanQueryBuilder SelectAll()
    {
        _select.Add("*");
        return this;
    }

    public SpanQueryBuilder Where(string attributeKey, string op, object value)
    {
        var param = $"${_paramIndex++}";
        _parameters[param] = value;
        _where.Add($"{GetColumnAccess(attributeKey)} {op} {param}");
        return this;
    }

    public SpanQueryBuilder WhereTimeRange(UnixNano start, UnixNano end)
    {
        var startParam = $"${_paramIndex++}";
        var endParam = $"${_paramIndex++}";
        _parameters[startParam] = start.Value;
        _parameters[endParam] = end.Value;
        _where.Add($"start_time_unix_nano >= {startParam}");
        _where.Add($"end_time_unix_nano <= {endParam}");
        return this;
    }

    public SpanQueryBuilder WhereSession(SessionId sessionId)
        => Where("session.id", "=", sessionId.Value);

    public SpanQueryBuilder WhereProvider(string provider)
        => Where("gen_ai.provider.name", "=", provider);

    public SpanQueryBuilder WhereModel(string model)
        => Where("gen_ai.request.model", "=", model);

    public SpanQueryBuilder WhereError()
    {
        _where.Add("status_code = 2");
        return this;
    }

    public SpanQueryBuilder GroupBy(params string[] columns)
    {
        foreach (var col in columns)
        {
            _groupBy.Add(GetColumnAccess(col));
        }

        return this;
    }

    public SpanQueryBuilder OrderBy(string column, bool descending = false)
    {
        var dir = descending ? "DESC" : "ASC";
        _orderBy.Add($"{GetColumnAccess(column)} {dir}");
        return this;
    }

    public SpanQueryBuilder Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    public SpanQueryBuilder Offset(int offset)
    {
        _offset = offset;
        return this;
    }

    public (string Sql, Dictionary<string, object> Parameters) Build()
    {
        var sql = new StringBuilder();

        sql.Append("SELECT ");
        sql.Append(_select.Count > 0 ? string.Join(", ", _select) : "*");
        sql.Append(" FROM spans");

        if (_where.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", _where));
        }

        if (_groupBy.Count > 0)
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", _groupBy));
        }

        if (_orderBy.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(string.Join(", ", _orderBy));
        }

        if (_limit.HasValue)
        {
            sql.Append($" LIMIT {_limit.Value}");
        }

        if (_offset.HasValue)
        {
            sql.Append($" OFFSET {_offset.Value}");
        }

        return (sql.ToString(), _parameters);
    }

    /// <summary>
    /// Returns column name for promoted fields, MAP access for others.
    /// </summary>
    private static string GetColumnAccess(string attributeKey)
    {
        // Core span fields
        if (attributeKey is "trace_id" or "span_id" or "parent_span_id" or
            "name" or "kind" or "status_code" or "status_message" or
            "start_time_unix_nano" or "end_time_unix_nano" or "duration_ns")
        {
            return attributeKey;
        }

        // Promoted fields - direct column access
        if (PromotedFields.IsPromoted(attributeKey))
        {
            return $"\"{attributeKey}\"";
        }

        // Non-promoted - MAP access
        return $"attributes['{attributeKey}']";
    }
}

// =============================================================================
// PART 7: TypeSpec-Aligned Attribute Value Types
// =============================================================================

/// <summary>
/// OTel-compliant attribute value (homogeneous arrays only).
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
/// Extended attribute value for GenAI content (allows nested structures).
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

// Query with automatic promoted field optimization
var (sql, parameters) = new SpanQueryBuilder()
    .Select("trace_id", "span_id", "gen_ai.request.model", "gen_ai.usage.input_tokens")
    .WhereProvider("anthropic")
    .WhereTimeRange(startTime, endTime)
    .OrderBy("start_time_unix_nano", descending: true)
    .Limit(100)
    .Build();

// Generated SQL uses promoted columns directly:
// SELECT trace_id, span_id, "gen_ai.request.model", "gen_ai.usage.input_tokens"
// FROM spans
// WHERE "gen_ai.provider.name" = $1
//   AND start_time_unix_nano >= $2
//   AND end_time_unix_nano <= $3
// ORDER BY start_time_unix_nano DESC
// LIMIT 100

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
