using qyl.collector.Services;

namespace qyl.collector.Storage;

public sealed partial class DuckDbStore
{
    // ══════════════════════════════════════════════════════════════════════════
    // SERVICE REGISTRY
    // ══════════════════════════════════════════════════════════════════════════

    internal const string ServiceInstancesDdl = """
        CREATE TABLE IF NOT EXISTS service_instances (
            service_namespace        VARCHAR NOT NULL DEFAULT '',
            service_name             VARCHAR NOT NULL,
            service_instance_id      VARCHAR NOT NULL,
            service_type             VARCHAR NOT NULL DEFAULT 'traditional',
            service_version          VARCHAR,
            deployment_environment   VARCHAR,
            os_type                  VARCHAR,
            host_arch                VARCHAR,
            agent_name               VARCHAR,
            provider_name            VARCHAR,
            default_model            VARCHAR,
            first_seen               TIMESTAMP NOT NULL,
            last_seen                TIMESTAMP NOT NULL,
            last_error_at            TIMESTAMP,
            status                   VARCHAR NOT NULL DEFAULT 'active',
            total_spans              BIGINT NOT NULL DEFAULT 0,
            total_logs               BIGINT NOT NULL DEFAULT 0,
            total_errors             BIGINT NOT NULL DEFAULT 0,
            total_input_tokens       BIGINT DEFAULT 0,
            total_output_tokens      BIGINT DEFAULT 0,
            total_cost_usd           DOUBLE DEFAULT 0,
            total_duration_ns        BIGINT DEFAULT 0,
            metadata                 JSON,
            PRIMARY KEY (service_namespace, service_name, service_type, service_instance_id)
        )
        """;

    internal const string ServicesViewDdl = """
        CREATE OR REPLACE VIEW services AS
        SELECT
            service_namespace,
            service_name,
            service_type,
            arg_max(service_version, last_seen) AS latest_version,
            arg_max(provider_name, last_seen) FILTER (WHERE provider_name IS NOT NULL) AS provider_name,
            arg_max(default_model, last_seen) FILTER (WHERE default_model IS NOT NULL) AS default_model,
            MIN(first_seen) AS first_seen,
            MAX(last_seen) AS last_seen,
            MAX(last_error_at) AS last_error_at,
            COUNT(*) AS total_instances,
            COUNT(*) FILTER (WHERE status = 'active') AS active_instances,
            array_agg(DISTINCT deployment_environment) FILTER (WHERE deployment_environment IS NOT NULL) AS environments,
            array_agg(DISTINCT service_version) FILTER (WHERE service_version IS NOT NULL) AS versions_seen,
            SUM(total_spans) AS total_spans,
            SUM(total_logs) AS total_logs,
            SUM(total_errors) AS total_errors,
            SUM(total_input_tokens) AS total_input_tokens,
            SUM(total_output_tokens) AS total_output_tokens,
            SUM(total_cost_usd) AS total_cost_usd,
            SUM(total_duration_ns) AS total_duration_ns,
            SUM(total_duration_ns) / NULLIF(SUM(total_spans), 0) AS avg_duration_ns,
            SUM(total_errors)::DOUBLE / NULLIF(SUM(total_spans) + SUM(total_logs), 0) AS error_rate
        FROM service_instances
        GROUP BY service_namespace, service_name, service_type
        """;

    private static DateTime NanoToDateTime(ulong nanos) =>
        TimeConversions.UnixNanoToDateTime(nanos);

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
            var ts = NanoToDateTime(instance.TimestampNano);
            cmd.Parameters.Add(new DuckDBParameter { Value = ts });
            cmd.Parameters.Add(new DuckDBParameter { Value = ts });
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
            qb.Add("service_type = $N", typeFilter);

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
            LIMIT {limit}
            """;
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
                FirstSeen = reader.Col(6).AsDateTimeOffset ?? default,
                LastSeen = reader.Col(7).AsDateTimeOffset ?? default,
                LastErrorAt = reader.Col(8).AsDateTimeOffset,
                TotalInstances = reader.Col(9).GetInt32(0),
                ActiveInstances = reader.Col(10).GetInt32(0),
                TotalSpans = reader.Col(13).GetInt64(0),
                TotalLogs = reader.Col(14).GetInt64(0),
                TotalErrors = reader.Col(15).GetInt64(0),
                TotalInputTokens = reader.Col(16).GetInt64(0),
                TotalOutputTokens = reader.Col(17).GetInt64(0),
                TotalCostUsd = reader.Col(18).GetDouble(0),
                ErrorRate = reader.Col(21).AsDouble
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
                FirstSeen = reader.Col(11).AsDateTimeOffset ?? default,
                LastSeen = reader.Col(12).AsDateTimeOffset ?? default,
                LastErrorAt = reader.Col(13).AsDateTimeOffset,
                Status = reader.Col(14).AsString ?? "active",
                TotalSpans = reader.Col(15).GetInt64(0),
                TotalLogs = reader.Col(16).GetInt64(0),
                TotalErrors = reader.Col(17).GetInt64(0),
                TotalInputTokens = reader.Col(18).GetInt64(0),
                TotalOutputTokens = reader.Col(19).GetInt64(0),
                TotalCostUsd = reader.Col(20).GetDouble(0)
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
        var job = new WriteJob<int>(static async (con, token) =>
        {
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
    ///     Backfills service_instances from spans for services not yet registered.
    /// </summary>
    public async Task BackfillMissingServicesAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        var job = new WriteJob<int>(static async (con, token) =>
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
                    make_timestamp((MIN(s.start_time_unix_nano) / 1000)::BIGINT) AS first_seen,
                    make_timestamp((MAX(s.start_time_unix_nano) / 1000)::BIGINT) AS last_seen,
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
