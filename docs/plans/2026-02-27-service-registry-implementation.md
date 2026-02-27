# Telemetry-Derived Service Registry — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Auto-detect services from OTLP telemetry, classify them by type (ai_agent, llm_provider, mcp_server, traditional), and expose via REST + MCP.

**Architecture:** Two-tier DuckDB design — `service_instances` physical table (upserted at ingest) + `services` virtual view (aggregates across instances). Background worker recomputes aggregates and status every 5 minutes.

**Tech Stack:** .NET 10.0 LTS, C# 14, DuckDB, ASP.NET Core Minimal API, MCP (ModelContextProtocol)

**Design doc:** `docs/plans/2026-02-27-service-registry-design.md`

---

### Task 1: DuckDB Migration — Create service_instances Table + services View

**Files:**
- Create: `src/qyl.collector/Storage/Migrations/V20260227__add_service_registry.sql`

**Step 1: Create the migration SQL file**

```sql
-- Service Registry: Telemetry-derived service auto-detection
-- Design: docs/plans/2026-02-27-service-registry-design.md

CREATE TABLE IF NOT EXISTS service_instances (
    -- Identity (composite PK follows OTel identity)
    service_namespace        VARCHAR NOT NULL DEFAULT '',
    service_name             VARCHAR NOT NULL,
    service_instance_id      VARCHAR NOT NULL,
    service_type             VARCHAR NOT NULL DEFAULT 'traditional',

    -- Resource attributes (OTel 1.40.0)
    service_version          VARCHAR,
    deployment_environment   VARCHAR,
    os_type                  VARCHAR,
    host_arch                VARCHAR,

    -- AI-specific (null for traditional)
    agent_name               VARCHAR,
    provider_name            VARCHAR,
    default_model            VARCHAR,

    -- Lifecycle
    first_seen               TIMESTAMP_NS NOT NULL,
    last_seen                TIMESTAMP_NS NOT NULL,
    last_error_at            TIMESTAMP_NS,
    status                   VARCHAR NOT NULL DEFAULT 'active',

    -- Aggregates (populated by background worker)
    total_spans              BIGINT NOT NULL DEFAULT 0,
    total_logs               BIGINT NOT NULL DEFAULT 0,
    total_errors             BIGINT NOT NULL DEFAULT 0,
    total_input_tokens       BIGINT DEFAULT 0,
    total_output_tokens      BIGINT DEFAULT 0,
    total_cost_usd           DOUBLE DEFAULT 0,
    total_duration_ns        BIGINT DEFAULT 0,

    -- Catch-all for unpromoted resource attributes
    metadata                 JSON,

    PRIMARY KEY (service_namespace, service_name, service_type, service_instance_id)
);

CREATE OR REPLACE VIEW services AS
SELECT
    service_namespace,
    service_name,
    service_type,
    arg_max(service_version, last_seen)                                                        AS latest_version,
    arg_max(provider_name, last_seen) FILTER (WHERE provider_name IS NOT NULL)                 AS provider_name,
    arg_max(default_model, last_seen) FILTER (WHERE default_model IS NOT NULL)                 AS default_model,
    MIN(first_seen)                                                                            AS first_seen,
    MAX(last_seen)                                                                             AS last_seen,
    MAX(last_error_at)                                                                         AS last_error_at,
    COUNT(*)                                                                                   AS total_instances,
    COUNT(*) FILTER (WHERE status = 'active')                                                  AS active_instances,
    array_agg(DISTINCT deployment_environment) FILTER (WHERE deployment_environment IS NOT NULL) AS environments,
    array_agg(DISTINCT service_version) FILTER (WHERE service_version IS NOT NULL)              AS versions_seen,
    SUM(total_spans)          AS total_spans,
    SUM(total_logs)           AS total_logs,
    SUM(total_errors)         AS total_errors,
    SUM(total_input_tokens)   AS total_input_tokens,
    SUM(total_output_tokens)  AS total_output_tokens,
    SUM(total_cost_usd)       AS total_cost_usd,
    SUM(total_duration_ns)    AS total_duration_ns,
    SUM(total_duration_ns) / NULLIF(SUM(total_spans), 0)                              AS avg_duration_ns,
    SUM(total_errors)::DOUBLE / NULLIF(SUM(total_spans) + SUM(total_logs), 0)         AS error_rate
FROM service_instances
GROUP BY service_namespace, service_name, service_type;
```

**Step 2: Verify migration runs**

Run: `dotnet build src/qyl.collector --no-restore`
Expected: Build succeeds (migration is SQL, just needs to be a valid embedded resource or file).

**Step 3: Commit**

```bash
git add src/qyl.collector/Storage/Migrations/V20260227__add_service_registry.sql
git commit -m "feat(storage): add service_instances table + services view migration"
```

---

### Task 2: ServiceClassifier — Attribute-Based Type Classification

**Files:**
- Create: `src/qyl.collector/Services/ServiceClassifier.cs`

**Step 1: Create the classifier**

Implements the priority-ordered classification from the design doc:

| Priority | Condition | Type |
|----------|-----------|------|
| 1 | `meter.name == "com.anthropic.claude_code"` or `event_name` starts with `claude_code.` | `ai_agent` |
| 2 | Any span attribute starts with `gen_ai.agent.` | `ai_agent` |
| 3 | Any span attribute starts with `mcp.` | `mcp_server` |
| 4 | `gen_ai.provider.name` present (resource or span) | `llm_provider` |
| 5 | Default | `traditional` |

```csharp
namespace qyl.collector.Services;

/// <summary>
///     Classifies services by type using OTel attribute inspection.
///     Priority-ordered rules — first match wins.
/// </summary>
public static class ServiceClassifier
{
    public const string TypeAiAgent = "ai_agent";
    public const string TypeMcpServer = "mcp_server";
    public const string TypeLlmProvider = "llm_provider";
    public const string TypeTraditional = "traditional";

    /// <summary>
    ///     Classifies a service based on resource + span attributes.
    /// </summary>
    /// <param name="resourceAttributes">Resource-level attributes (may be null).</param>
    /// <param name="spanAttributes">Span-level attributes (may be null).</param>
    public static string Classify(
        IReadOnlyDictionary<string, string>? resourceAttributes,
        IReadOnlyDictionary<string, string>? spanAttributes)
    {
        // P1: Claude Code detection
        if (IsClaudeCode(resourceAttributes, spanAttributes))
            return TypeAiAgent;

        // P2: Generic AI agent (gen_ai.agent.* attributes)
        if (HasPrefixKey(spanAttributes, "gen_ai.agent."))
            return TypeAiAgent;

        // P3: MCP server (mcp.* attributes)
        if (HasPrefixKey(spanAttributes, "mcp."))
            return TypeMcpServer;

        // P4: LLM provider (gen_ai.provider.name present anywhere)
        if (HasKey(resourceAttributes, "gen_ai.provider.name") ||
            HasKey(spanAttributes, "gen_ai.provider.name"))
            return TypeLlmProvider;

        // P5: Default
        return TypeTraditional;
    }

    private static bool IsClaudeCode(
        IReadOnlyDictionary<string, string>? resource,
        IReadOnlyDictionary<string, string>? span)
    {
        if (resource is not null &&
            resource.TryGetValue("meter.name", out var meterName) &&
            meterName == "com.anthropic.claude_code")
            return true;

        if (span is null) return false;

        foreach (var kvp in span)
        {
            if (kvp.Key.StartsWith("claude_code.", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool HasPrefixKey(
        IReadOnlyDictionary<string, string>? attrs,
        string prefix)
    {
        if (attrs is null) return false;

        foreach (var kvp in attrs)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool HasKey(
        IReadOnlyDictionary<string, string>? attrs,
        string key) =>
        attrs is not null && attrs.ContainsKey(key);
}
```

**Step 2: Build**

Run: `dotnet build src/qyl.collector --no-restore`
Expected: 0 errors, 0 warnings

**Step 3: Commit**

```bash
git add src/qyl.collector/Services/ServiceClassifier.cs
git commit -m "feat(services): add attribute-based service type classifier"
```

---

### Task 3: DuckDbStore.Services.cs — Upsert + Query Methods

**Files:**
- Create: `src/qyl.collector/Storage/DuckDbStore.Services.cs`

**Step 1: Create the partial class with upsert + query methods**

This file provides:
1. `UpsertServiceInstanceAsync` — called at ingest time (idempotent INSERT ON CONFLICT)
2. `GetServicesAsync` — queries the `services` view for the REST endpoint
3. `GetServiceDetailAsync` — single service with its instance list
4. `UpdateServiceAggregatesAsync` — called by background worker to recompute aggregates

```csharp
using System.Text.Json;
using qyl.collector.Services;

namespace qyl.collector.Storage;

public sealed partial class DuckDbStore
{
    // ══════════════════════════════════════════════════════════════════════════
    // SERVICE REGISTRY
    // ══════════════════════════════════════════════════════════════════════════

    private const string ServiceInstanceUpsertSql = """
        INSERT INTO service_instances (
            service_namespace, service_name, service_instance_id, service_type,
            service_version, deployment_environment, os_type, host_arch,
            agent_name, provider_name, default_model,
            first_seen, last_seen, status, metadata
        ) VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, 'active', $14)
        ON CONFLICT (service_namespace, service_name, service_type, service_instance_id)
        DO UPDATE SET
            service_version        = COALESCE(EXCLUDED.service_version, service_instances.service_version),
            deployment_environment = COALESCE(EXCLUDED.deployment_environment, service_instances.deployment_environment),
            os_type                = COALESCE(EXCLUDED.os_type, service_instances.os_type),
            host_arch              = COALESCE(EXCLUDED.host_arch, service_instances.host_arch),
            agent_name             = COALESCE(EXCLUDED.agent_name, service_instances.agent_name),
            provider_name          = COALESCE(EXCLUDED.provider_name, service_instances.provider_name),
            default_model          = COALESCE(EXCLUDED.default_model, service_instances.default_model),
            last_seen              = GREATEST(EXCLUDED.last_seen, service_instances.last_seen),
            status                 = 'active',
            metadata               = COALESCE(EXCLUDED.metadata, service_instances.metadata)
        """;

    /// <summary>
    ///     Upserts a service instance discovered from OTLP resource attributes.
    ///     Called at ingest time — idempotent and cheap.
    /// </summary>
    public async Task UpsertServiceInstanceAsync(ServiceInstanceRecord instance, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = ServiceInstanceUpsertSql;
            cmd.Parameters.Add(new DuckDBParameter { Value = instance.ServiceNamespace });
            cmd.Parameters.Add(new DuckDBParameter { Value = instance.ServiceName });
            cmd.Parameters.Add(new DuckDBParameter { Value = instance.ServiceInstanceId });
            cmd.Parameters.Add(new DuckDBParameter { Value = instance.ServiceType });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.ServiceVersion ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.DeploymentEnvironment ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.OsType ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.HostArch ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.AgentName ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.ProviderName ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.DefaultModel ?? DBNull.Value });
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)instance.TimestampNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = (decimal)instance.TimestampNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = (object?)instance.MetadataJson ?? DBNull.Value });
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Queries the services view for the REST endpoint.
    /// </summary>
    public async Task<IReadOnlyList<ServiceSummary>> GetServicesAsync(
        string? typeFilter = null,
        string? statusFilter = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        var qb = new QueryBuilder();
        if (typeFilter is not null)
        {
            qb.AddCondition($"service_type = ${qb.NextParam}");
            qb.Add(new DuckDBParameter { Value = typeFilter });
        }

        // Status filter requires a subquery since 'services' view doesn't have per-row status
        // Filter on the aggregated active_instances instead
        if (statusFilter == "active")
            qb.AddCondition("active_instances > 0");
        else if (statusFilter == "inactive")
            qb.AddCondition("active_instances = 0");

        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT
                service_namespace, service_name, service_type,
                latest_version, provider_name, default_model,
                first_seen, last_seen, last_error_at,
                total_instances, active_instances,
                environments, versions_seen,
                total_spans, total_logs, total_errors,
                total_input_tokens, total_output_tokens,
                total_cost_usd, total_duration_ns,
                avg_duration_ns, error_rate
            FROM services
            {qb.WhereClause}
            ORDER BY last_seen DESC
            LIMIT $limit
            """.Replace("$limit", limit.ToString());
        qb.ApplyTo(cmd);

        var results = new List<ServiceSummary>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(new ServiceSummary
            {
                ServiceNamespace = reader.Col(0).AsString ?? "",
                ServiceName = reader.Col(1).AsString ?? "",
                ServiceType = reader.Col(2).AsString ?? "traditional",
                LatestVersion = reader.Col(3).AsString,
                ProviderName = reader.Col(4).AsString,
                DefaultModel = reader.Col(5).AsString,
                FirstSeen = reader.GetDateTime(6),
                LastSeen = reader.GetDateTime(7),
                LastErrorAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                TotalInstances = reader.Col(9).GetInt32(0),
                ActiveInstances = reader.Col(10).GetInt32(0),
                TotalSpans = reader.Col(13).GetInt64(0),
                TotalLogs = reader.Col(14).GetInt64(0),
                TotalErrors = reader.Col(15).GetInt64(0),
                TotalInputTokens = reader.Col(16).GetInt64(0),
                TotalOutputTokens = reader.Col(17).GetInt64(0),
                TotalCostUsd = reader.IsDBNull(18) ? 0 : reader.GetDouble(18),
                ErrorRate = reader.IsDBNull(21) ? null : reader.GetDouble(21)
            });
        }

        return results;
    }

    /// <summary>
    ///     Gets a single service with its instance list.
    /// </summary>
    public async Task<ServiceDetail?> GetServiceDetailAsync(
        string serviceName,
        string? serviceType = null,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await using var lease = await RentReadAsync(ct).ConfigureAwait(false);

        // Get instances
        await using var cmd = lease.Connection.CreateCommand();
        var typeClause = serviceType is not null ? "AND service_type = $2" : "";
        cmd.CommandText = $"""
            SELECT
                service_namespace, service_name, service_instance_id, service_type,
                service_version, deployment_environment, os_type, host_arch,
                agent_name, provider_name, default_model,
                first_seen, last_seen, last_error_at, status,
                total_spans, total_logs, total_errors,
                total_input_tokens, total_output_tokens, total_cost_usd
            FROM service_instances
            WHERE service_name = $1 {typeClause}
            ORDER BY last_seen DESC
            """;
        cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
        if (serviceType is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceType });

        var instances = new List<ServiceInstanceDto>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            instances.Add(new ServiceInstanceDto
            {
                ServiceNamespace = reader.Col(0).AsString ?? "",
                ServiceName = reader.Col(1).AsString ?? "",
                ServiceInstanceId = reader.Col(2).AsString ?? "",
                ServiceType = reader.Col(3).AsString ?? "traditional",
                ServiceVersion = reader.Col(4).AsString,
                DeploymentEnvironment = reader.Col(5).AsString,
                OsType = reader.Col(6).AsString,
                HostArch = reader.Col(7).AsString,
                AgentName = reader.Col(8).AsString,
                ProviderName = reader.Col(9).AsString,
                DefaultModel = reader.Col(10).AsString,
                FirstSeen = reader.GetDateTime(11),
                LastSeen = reader.GetDateTime(12),
                LastErrorAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                Status = reader.Col(14).AsString ?? "active",
                TotalSpans = reader.Col(15).GetInt64(0),
                TotalLogs = reader.Col(16).GetInt64(0),
                TotalErrors = reader.Col(17).GetInt64(0),
                TotalInputTokens = reader.Col(18).GetInt64(0),
                TotalOutputTokens = reader.Col(19).GetInt64(0),
                TotalCostUsd = reader.IsDBNull(20) ? 0 : reader.GetDouble(20)
            });
        }

        if (instances.Count is 0)
            return null;

        return new ServiceDetail
        {
            ServiceName = serviceName,
            ServiceType = instances[0].ServiceType,
            Instances = instances
        };
    }

    /// <summary>
    ///     Recomputes aggregates for all service instances from spans/logs tables.
    ///     Called by ServiceMaterializerService on a 5-minute interval.
    /// </summary>
    public async Task UpdateServiceAggregatesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            // Recompute from spans
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                UPDATE service_instances si SET
                    total_spans = agg.span_count,
                    total_errors = agg.error_count,
                    total_input_tokens = agg.input_tokens,
                    total_output_tokens = agg.output_tokens,
                    total_cost_usd = agg.cost_usd,
                    total_duration_ns = agg.duration_ns
                FROM (
                    SELECT
                        service_name,
                        COUNT(*) AS span_count,
                        COUNT(*) FILTER (WHERE status_code = 2) AS error_count,
                        COALESCE(SUM(gen_ai_input_tokens), 0) AS input_tokens,
                        COALESCE(SUM(gen_ai_output_tokens), 0) AS output_tokens,
                        COALESCE(SUM(gen_ai_cost_usd), 0) AS cost_usd,
                        COALESCE(SUM(duration_ns), 0) AS duration_ns
                    FROM spans
                    GROUP BY service_name
                ) agg
                WHERE si.service_name = agg.service_name
                """;
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Update log counts
            await using var logCmd = con.CreateCommand();
            logCmd.CommandText = """
                UPDATE service_instances si SET
                    total_logs = agg.log_count
                FROM (
                    SELECT service_name, COUNT(*) AS log_count
                    FROM logs
                    WHERE service_name IS NOT NULL
                    GROUP BY service_name
                ) agg
                WHERE si.service_name = agg.service_name
                """;
            await logCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            // Update status: active (seen within 5 min), inactive, degraded
            await using var statusCmd = con.CreateCommand();
            statusCmd.CommandText = """
                UPDATE service_instances SET status = CASE
                    WHEN last_seen >= (now() - INTERVAL '5 minutes') THEN
                        CASE WHEN total_errors::DOUBLE / NULLIF(total_spans + total_logs, 0) > 0.1
                             THEN 'degraded' ELSE 'active' END
                    ELSE 'inactive'
                END
                """;
            await statusCmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            return 0;
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }

    /// <summary>
    ///     Backfills service_instances from spans/logs for services not yet registered.
    ///     Called by ServiceMaterializerService after aggregate update.
    /// </summary>
    public async Task BackfillMissingServicesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(async (con, token) =>
        {
            await using var cmd = con.CreateCommand();
            cmd.CommandText = """
                INSERT INTO service_instances (
                    service_namespace, service_name, service_instance_id, service_type,
                    first_seen, last_seen, status
                )
                SELECT
                    '' AS service_namespace,
                    s.service_name,
                    'unknown' AS service_instance_id,
                    'traditional' AS service_type,
                    make_timestamp(MIN(s.start_time_unix_nano) / 1000) AS first_seen,
                    make_timestamp(MAX(s.start_time_unix_nano) / 1000) AS last_seen,
                    'active' AS status
                FROM spans s
                WHERE s.service_name IS NOT NULL
                  AND s.service_name != 'unknown'
                  AND NOT EXISTS (
                      SELECT 1 FROM service_instances si
                      WHERE si.service_name = s.service_name
                  )
                GROUP BY s.service_name
                ON CONFLICT DO NOTHING
                """;
            return await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        });

        await _jobs.Writer.WriteAsync(job, ct).ConfigureAwait(false);
        await job.Task.ConfigureAwait(false);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// RECORD TYPES
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
///     Data extracted from OTLP resource attributes for the service upsert.
/// </summary>
public sealed record ServiceInstanceRecord
{
    public required string ServiceNamespace { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceInstanceId { get; init; }
    public required string ServiceType { get; init; }
    public string? ServiceVersion { get; init; }
    public string? DeploymentEnvironment { get; init; }
    public string? OsType { get; init; }
    public string? HostArch { get; init; }
    public string? AgentName { get; init; }
    public string? ProviderName { get; init; }
    public string? DefaultModel { get; init; }
    public ulong TimestampNano { get; init; }
    public string? MetadataJson { get; init; }
}
```

**Step 2: Build**

Run: `dotnet build src/qyl.collector --no-restore`
Expected: 0 errors, 0 warnings

**Step 3: Commit**

```bash
git add src/qyl.collector/Storage/DuckDbStore.Services.cs
git commit -m "feat(storage): add service registry upsert, query, and aggregate methods"
```

---

### Task 4: ServiceMaterializerService — Background Aggregate Worker

**Files:**
- Create: `src/qyl.collector/Services/ServiceMaterializerService.cs`

**Step 1: Create the background service**

Follows `InsightsMaterializerService` pattern exactly: `BackgroundService`, 5-minute `PeriodicTimer`, `TimeProvider` for testability.

```csharp
namespace qyl.collector.Services;

/// <summary>
///     Background service that recomputes service_instances aggregates
///     from spans/logs tables every 5 minutes.
/// </summary>
public sealed partial class ServiceMaterializerService(
    DuckDbStore store,
    ILogger<ServiceMaterializerService> logger,
    TimeProvider? timeProvider = null)
    : BackgroundService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for initial data to arrive
        await Task.Delay(TimeSpan.FromSeconds(30), _timeProvider, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await MaterializeAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogMaterializationError(ex);
            }
        }
    }

    private async Task MaterializeAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // 1. Backfill services found in spans/logs but missing from service_instances
        await store.BackfillMissingServicesAsync(ct).ConfigureAwait(false);

        // 2. Recompute aggregates from spans/logs
        await store.UpdateServiceAggregatesAsync(ct).ConfigureAwait(false);

        sw.Stop();
        LogMaterialized(sw.Elapsed.TotalMilliseconds);
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Error during service registry materialization")]
    private partial void LogMaterializationError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Service registry materialized in {DurationMs:F1}ms")]
    private partial void LogMaterialized(double durationMs);
}
```

**Step 2: Build**

Run: `dotnet build src/qyl.collector --no-restore`
Expected: 0 errors, 0 warnings

**Step 3: Commit**

```bash
git add src/qyl.collector/Services/ServiceMaterializerService.cs
git commit -m "feat(services): add background aggregate materialization worker"
```

---

### Task 5: ServiceEndpoints — REST Endpoints + DTOs + JSON Context

**Files:**
- Create: `src/qyl.collector/Services/ServiceEndpoints.cs`

**Step 1: Create endpoints + DTOs + JSON context**

Follows `ClaudeCodeEndpoints.cs` and `CopilotEndpoints.cs` pattern:

```csharp
using System.Text.Json.Serialization;

namespace qyl.collector.Services;

internal static class ServiceEndpoints
{
    public static WebApplication MapServiceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/services");

        group.MapGet("/", GetServicesAsync);
        group.MapGet("/{serviceName}", GetServiceDetailAsync);

        return app;
    }

    private static async Task<IResult> GetServicesAsync(
        DuckDbStore store,
        string? type,
        string? status,
        int? limit,
        CancellationToken ct)
    {
        var services = await store.GetServicesAsync(
            type, status, limit ?? 100, ct).ConfigureAwait(false);

        return Results.Ok(new ServicesResponse
        {
            Services = services,
            Total = services.Count
        });
    }

    private static async Task<IResult> GetServiceDetailAsync(
        string serviceName,
        DuckDbStore store,
        string? type,
        CancellationToken ct)
    {
        var detail = await store.GetServiceDetailAsync(serviceName, type, ct).ConfigureAwait(false);

        if (detail is null)
            return Results.NotFound();

        return Results.Ok(detail);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// DTOs
// ═════════════════════════════════════════════════════════════════════════════

public sealed record ServiceSummary
{
    public required string ServiceNamespace { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceType { get; init; }
    public string? LatestVersion { get; init; }
    public string? ProviderName { get; init; }
    public string? DefaultModel { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public int TotalInstances { get; init; }
    public int ActiveInstances { get; init; }
    public long TotalSpans { get; init; }
    public long TotalLogs { get; init; }
    public long TotalErrors { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
    public double? ErrorRate { get; init; }
}

public sealed record ServiceInstanceDto
{
    public required string ServiceNamespace { get; init; }
    public required string ServiceName { get; init; }
    public required string ServiceInstanceId { get; init; }
    public required string ServiceType { get; init; }
    public string? ServiceVersion { get; init; }
    public string? DeploymentEnvironment { get; init; }
    public string? OsType { get; init; }
    public string? HostArch { get; init; }
    public string? AgentName { get; init; }
    public string? ProviderName { get; init; }
    public string? DefaultModel { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public string Status { get; init; } = "active";
    public long TotalSpans { get; init; }
    public long TotalLogs { get; init; }
    public long TotalErrors { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public double TotalCostUsd { get; init; }
}

public sealed record ServiceDetail
{
    public required string ServiceName { get; init; }
    public required string ServiceType { get; init; }
    public required IReadOnlyList<ServiceInstanceDto> Instances { get; init; }
}

// ═════════════════════════════════════════════════════════════════════════════
// Response wrappers
// ═════════════════════════════════════════════════════════════════════════════

internal sealed record ServicesResponse
{
    public required IReadOnlyList<ServiceSummary> Services { get; init; }
    public int Total { get; init; }
}

// ═════════════════════════════════════════════════════════════════════════════
// JSON serializer context (AOT-compatible)
// ═════════════════════════════════════════════════════════════════════════════

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ServiceSummary))]
[JsonSerializable(typeof(ServiceInstanceDto))]
[JsonSerializable(typeof(ServiceDetail))]
[JsonSerializable(typeof(ServicesResponse))]
internal sealed partial class ServiceSerializerContext : JsonSerializerContext;
```

**Step 2: Build**

Run: `dotnet build src/qyl.collector --no-restore`
Expected: 0 errors, 0 warnings

**Step 3: Commit**

```bash
git add src/qyl.collector/Services/ServiceEndpoints.cs
git commit -m "feat(services): add REST endpoints, DTOs, and JSON context"
```

---

### Task 6: OtlpConverter — Extract Resource Attributes + Call Upsert

**Files:**
- Modify: `src/qyl.collector/Ingestion/OtlpConverter.cs`

**Step 1: Add resource attribute extraction**

Three changes to `OtlpConverter.cs`:

1. **Proto path** (`ConvertProtoToStorageRows`): Extract all resource attributes into a dictionary per `resourceSpan`, serialize to `resource_json`, and return alongside spans.

2. **JSON path** (`ConvertJsonToStorageRows`): Same extraction using `resourceSpan.Resource?.Attributes`.

3. **Shared `CreateStorageRow`**: Accept `resourceJson` parameter instead of hardcoding `null`.

The converter is a static class — it produces `ServiceInstanceRecord` alongside spans. The caller (Program.cs ingest endpoints) is responsible for calling `DuckDbStore.UpsertServiceInstanceAsync`.

Add a new method:

```csharp
/// <summary>
///     Extracts a ServiceInstanceRecord from OTLP resource attributes.
///     Returns null if service.name is "unknown" or missing.
/// </summary>
public static ServiceInstanceRecord? ExtractServiceInstance(
    IReadOnlyDictionary<string, string> resourceAttributes,
    IReadOnlyDictionary<string, string>? spanAttributes,
    ulong timestampNano)
{
    if (!resourceAttributes.TryGetValue("service.name", out var serviceName) ||
        serviceName is "unknown" or "")
        return null;

    var serviceType = ServiceClassifier.Classify(resourceAttributes, spanAttributes);

    return new ServiceInstanceRecord
    {
        ServiceNamespace = resourceAttributes.GetValueOrDefault("service.namespace") ?? "",
        ServiceName = serviceName,
        ServiceInstanceId = resourceAttributes.GetValueOrDefault("service.instance.id")
                            ?? Environment.MachineName,
        ServiceType = serviceType,
        ServiceVersion = resourceAttributes.GetValueOrDefault("service.version"),
        DeploymentEnvironment = resourceAttributes.GetValueOrDefault("deployment.environment.name")
                                ?? resourceAttributes.GetValueOrDefault("deployment.environment"),
        OsType = resourceAttributes.GetValueOrDefault("os.type"),
        HostArch = resourceAttributes.GetValueOrDefault("host.arch"),
        AgentName = resourceAttributes.GetValueOrDefault("gen_ai.agent.name"),
        ProviderName = resourceAttributes.GetValueOrDefault("gen_ai.provider.name"),
        DefaultModel = resourceAttributes.GetValueOrDefault("gen_ai.request.model"),
        TimestampNano = timestampNano
    };
}
```

Also modify `CreateStorageRow` to accept and pass through a `resourceJson` string parameter instead of always setting `ResourceJson = null`.

**Step 2: Fix resource_json population**

In `ConvertProtoToStorageRows`: serialize `resourceSpan.Resource.Attributes` to JSON and pass to `CreateStorageRowFromProto`.

In `ConvertJsonToStorageRows`: serialize `resourceSpan.Resource?.Attributes` and pass through.

**Step 3: Build**

Run: `dotnet build src/qyl.collector --no-restore`
Expected: 0 errors, 0 warnings

**Step 4: Commit**

```bash
git add src/qyl.collector/Ingestion/OtlpConverter.cs
git commit -m "feat(ingest): extract resource attributes, populate resource_json, add service extraction"
```

---

### Task 7: Program.cs (Collector) — Register Services + Call Upsert at Ingest

**Files:**
- Modify: `src/qyl.collector/Program.cs`

**Step 1: Add service registration**

Add near other `AddHostedService` calls (around line 163):

```csharp
using qyl.collector.Services;

// ... existing registrations ...
builder.Services.AddHostedService<ServiceMaterializerService>();
```

**Step 2: Register endpoints**

Add after `app.MapClaudeCodeEndpoints();` (around line 304):

```csharp
app.MapServiceEndpoints();
```

**Step 3: Wire upsert into ingest paths**

In the POST `/v1/traces` handler and the gRPC `TraceServiceImpl`, after converting spans and before enqueuing the batch, extract the first span's resource attributes and call `UpsertServiceInstanceAsync`. The upsert is fire-and-forget — it goes through the write channel.

Find where `store.EnqueueAsync(new SpanBatch(...))` is called and add the service extraction before it. The resource attributes from the first span in each resource batch are sufficient for the upsert.

**Step 4: Build and test**

Run: `dotnet build src/qyl.collector --no-restore && dotnet test --project tests/qyl.collector.tests # VERIFY`
Expected: 0 errors, 0 warnings, all tests pass

**Step 5: Commit**

```bash
git add src/qyl.collector/Program.cs
git commit -m "feat: register service registry endpoints and background worker"
```

---

### Task 8: MCP ServiceTools — list_services Tool

**Files:**
- Create: `src/qyl.mcp/Tools/ServiceTools.cs`
- Modify: `src/qyl.mcp/Program.cs`

**Step 1: Create MCP tool**

Follows `CopilotTools.cs` pattern: HTTP client → collector API → formatted markdown.

```csharp
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace qyl.mcp.Tools;

[McpServerToolType]
public sealed class ServiceTools(HttpClient client)
{
    [McpServerTool(Name = "qyl.list_services")]
    [Description("List all detected services with type, status, instance count, and aggregated telemetry stats")]
    public Task<string> ListServicesAsync(
        [Description("Filter by service type: ai_agent, llm_provider, mcp_server, traditional")] string? type = null,
        [Description("Filter by status: active, inactive")] string? status = null,
        [Description("Maximum services to return (default: 50)")] int limit = 50) =>
        CollectorHelper.ExecuteAsync(async () =>
        {
            var query = $"?limit={limit}";
            if (type is not null) query += $"&type={type}";
            if (status is not null) query += $"&status={status}";

            var response = await client.GetFromJsonAsync<ServicesMcpResponse>(
                $"/api/v1/services{query}",
                ServiceMcpJsonContext.Default.ServicesMcpResponse).ConfigureAwait(false);

            if (response?.Services is not { Count: > 0 })
                return "No services detected yet. Services are auto-discovered from incoming OTLP telemetry.";

            var sb = new StringBuilder();
            sb.AppendLine($"## Detected Services ({response.Total})");
            sb.AppendLine();
            sb.AppendLine("| Service | Type | Status | Instances | Spans | Errors | Error Rate |");
            sb.AppendLine("|---------|------|--------|-----------|-------|--------|------------|");

            foreach (var s in response.Services)
            {
                var statusIndicator = s.ActiveInstances > 0 ? "active" : "inactive";
                var errorRate = s.ErrorRate.HasValue ? $"{s.ErrorRate.Value:P1}" : "—";
                sb.AppendLine($"| {s.ServiceName} | {s.ServiceType} | {statusIndicator} | {s.TotalInstances} | {s.TotalSpans:N0} | {s.TotalErrors:N0} | {errorRate} |");
            }

            return sb.ToString();
        });
}

// ═════════════════════════════════════════════════════════════════════════════
// MCP DTOs (separate from collector DTOs for AOT isolation)
// ═════════════════════════════════════════════════════════════════════════════

internal sealed record ServiceMcpSummary
{
    [JsonPropertyName("serviceNamespace")] public string ServiceNamespace { get; init; } = "";
    [JsonPropertyName("serviceName")] public string ServiceName { get; init; } = "";
    [JsonPropertyName("serviceType")] public string ServiceType { get; init; } = "traditional";
    [JsonPropertyName("latestVersion")] public string? LatestVersion { get; init; }
    [JsonPropertyName("providerName")] public string? ProviderName { get; init; }
    [JsonPropertyName("defaultModel")] public string? DefaultModel { get; init; }
    [JsonPropertyName("totalInstances")] public int TotalInstances { get; init; }
    [JsonPropertyName("activeInstances")] public int ActiveInstances { get; init; }
    [JsonPropertyName("totalSpans")] public long TotalSpans { get; init; }
    [JsonPropertyName("totalErrors")] public long TotalErrors { get; init; }
    [JsonPropertyName("errorRate")] public double? ErrorRate { get; init; }
}

internal sealed record ServicesMcpResponse
{
    [JsonPropertyName("services")] public IReadOnlyList<ServiceMcpSummary> Services { get; init; } = [];
    [JsonPropertyName("total")] public int Total { get; init; }
}

// ═════════════════════════════════════════════════════════════════════════════
// JSON Context (AOT)
// ═════════════════════════════════════════════════════════════════════════════

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ServiceMcpSummary))]
[JsonSerializable(typeof(ServicesMcpResponse))]
internal sealed partial class ServiceMcpJsonContext : JsonSerializerContext;
```

**Step 2: Register in MCP Program.cs**

Add these 3 lines to `src/qyl.mcp/Program.cs`:

```csharp
builder.Services.AddCollectorToolClient<ServiceTools>(collectorUrl);
// ... in jsonOptions setup:
jsonOptions.TypeInfoResolverChain.Add(ServiceMcpJsonContext.Default);
// ... in MCP builder chain:
.WithTools<ServiceTools>(jsonOptions)
```

**Step 3: Build**

Run: `dotnet build src/qyl.mcp --no-restore`
Expected: 0 errors, 0 warnings

**Step 4: Commit**

```bash
git add src/qyl.mcp/Tools/ServiceTools.cs src/qyl.mcp/Program.cs
git commit -m "feat(mcp): add qyl.list_services MCP tool"
```

---

### Task 9: Full Build + Test

**Step 1: Full build**

Run: `dotnet build`
Expected: 0 errors, 0 warnings

**Step 2: Run all tests**

Run: `dotnet test # VERIFY`
Expected: All tests pass

**Step 3: Manual verification**

Start collector, verify endpoints:

```bash
dotnet run --project src/qyl.collector &
sleep 3
curl -s http://localhost:5100/api/v1/services | jq .
# Expected: {"services":[],"total":0}

curl -s http://localhost:5100/api/v1/services/nonexistent
# Expected: 404
```

**Step 4: Final commit**

```bash
git add -A
git commit -m "feat: complete telemetry-derived service registry

- service_instances DuckDB table with composite PK (namespace, name, type, instance_id)
- services virtual view with arg_max, array_agg, error_rate aggregations
- Attribute-based service classification (ai_agent, llm_provider, mcp_server, traditional)
- Ingest-time idempotent upsert from OTLP resource attributes
- Background worker: 5-min aggregate recomputation + status updates + backfill
- REST: GET /api/v1/services (list), GET /api/v1/services/{name} (detail)
- MCP: qyl.list_services tool with markdown table output
- resource_json populated for spans (was always null)

Design: docs/plans/2026-02-27-service-registry-design.md"
```

---

## Implementation Sequence Summary

| Task | What | Gate |
|------|------|------|
| 1 | Migration SQL (table + view) | — |
| 2 | ServiceClassifier (attribute rules) | — |
| 3 | DuckDbStore.Services.cs (upsert + queries) | — |
| 4 | ServiceMaterializerService (background worker) | — |
| 5 | ServiceEndpoints (REST + DTOs + JSON context) | — |
| 6 | OtlpConverter (resource extraction + resource_json fix) | — |
| 7 | Program.cs collector (register + wire ingest) | `dotnet build src/qyl.collector` |
| 8 | ServiceTools MCP + Program.cs mcp | `dotnet build src/qyl.mcp` |
| 9 | Full build + test + manual verification | 0 errors, 0 warnings, all tests pass |

Tasks 1–6 have no dependencies and can be implemented in parallel.
Task 7 depends on 1–6.
Task 8 depends on 5 (DTOs).
Task 9 depends on all.
