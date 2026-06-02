using ANcpLua.Roslyn.Utilities.Time;

namespace Qyl.Collector.Metrics;

internal static class McpMetricsEndpoints
{
    [QylMapEndpoints]
    public static WebApplication MapMcpMetricsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/mcp/metrics");

        group.MapGet("", ListMetricsAsync);
        group.MapGet("/{name}/query", QueryMetricAsync);

        return app;
    }

    private static IResult ListMetricsAsync(string? project)
    {
        if (!string.IsNullOrWhiteSpace(project))
            return TypedResults.BadRequest(new McpMetricsError(
                "Project-scoped metrics are not available yet; omit project to list collector-wide derived metrics."));

        var metrics = GetMetricDefinitions()
            .Select(static metric => new McpMetricInfo(
                metric.Name,
                metric.Type,
                metric.Description,
                metric.Unit,
                GetLabelKeys(metric)));

        return TypedResults.Ok(metrics);
    }

    private static async Task<IResult> QueryMetricAsync(
        string name,
        DuckDbStore store,
        string? filter,
        string? from,
        string? to,
        string? interval,
        string? tokenType,
        CancellationToken ct)
    {
        var metric = FindMetric(name);
        if (metric is null)
            return TypedResults.NotFound(new McpMetricsError($"Unknown metric '{name}'."));

        if (!TryResolveWindow(from, to, out var start, out var end, out var windowError))
            return TypedResults.BadRequest(new McpMetricsError(windowError));

        if (!TryResolveInterval(interval, out var intervalSql, out var intervalError))
            return TypedResults.BadRequest(new McpMetricsError(intervalError));

        if (!TryResolveFilter(filter, out var serviceName, out var filterError))
            return TypedResults.BadRequest(new McpMetricsError(filterError));

        if (!TryResolveTokenType(metric, tokenType, out var resolvedTokenType, out var tokenTypeError))
            return TypedResults.BadRequest(new McpMetricsError(tokenTypeError));

        var points = resolvedTokenType is null
            ? await QueryMetricPointsAsync(
                store,
                metric,
                start,
                end,
                intervalSql,
                serviceName,
                ct).ConfigureAwait(false)
            : await QueryGenAiTokenUsagePointsAsync(
                store,
                start,
                end,
                intervalSql,
                serviceName,
                resolvedTokenType,
                ct).ConfigureAwait(false);

        return TypedResults.Ok(new McpMetricSeries(name, CreateLabels(serviceName, resolvedTokenType), points));
    }

    internal static IEnumerable<DerivedMetricDefinition> GetMetricDefinitions()
    {
        return DerivedMetricCatalog.GetDefinitions();
    }

    internal static DerivedMetricDefinition? FindMetric(string name)
    {
        return DerivedMetricCatalog.Find(name);
    }

    internal static IReadOnlyList<string> GetLabelKeys(DerivedMetricDefinition metric)
    {
        if (IsGenAiTokenUsageMetric(metric))
            return
            [
                DerivedMetricCatalog.ServiceNameLabel,
                DerivedMetricCatalog.GenAiProviderNameLabel,
                DerivedMetricCatalog.GenAiRequestModelLabel,
                DerivedMetricCatalog.GenAiTokenTypeLabel
            ];

        if (SupportsGenAiDimensions(metric))
        {
            return
            [
                DerivedMetricCatalog.ServiceNameLabel,
                DerivedMetricCatalog.GenAiProviderNameLabel,
                DerivedMetricCatalog.GenAiRequestModelLabel
            ];
        }

        return [DerivedMetricCatalog.ServiceNameLabel];
    }

    internal static Task<List<McpMetricPoint>> QueryMetricPointsAsync(
        DuckDbStore store,
        DerivedMetricDefinition metric,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        string? serviceName,
        CancellationToken ct)
    {
        var startNano = TimeConversions.ToUnixNano(start);
        var endNano = TimeConversions.ToUnixNano(end);
        var where = string.Concat(
            "start_time_unix_nano >= $1 AND start_time_unix_nano < $2",
            metric.Predicate is null ? string.Empty : $" AND ({metric.Predicate})",
            serviceName is null ? string.Empty : " AND service_name = $3");

        return store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = string.Create(CultureInfo.InvariantCulture, $"""
                SELECT
                    time_bucket({intervalSql}, to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                    CAST(({metric.Expression}) AS DOUBLE) AS metric_value
                FROM spans
                WHERE {where}
                GROUP BY bucket
                ORDER BY bucket ASC
                """);
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
            if (serviceName is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });

            List<McpMetricPoint> points = [];
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                points.Add(new McpMetricPoint(
                    reader.GetDateTime(0).ToString("O", CultureInfo.InvariantCulture),
                    reader.GetDouble(1)));
            }

            return points;
        }, ct);
    }

    internal static Task<List<McpMetricServicePoint>> QueryMetricPointsByServiceAsync(
        DuckDbStore store,
        DerivedMetricDefinition metric,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        string? serviceName,
        CancellationToken ct)
    {
        var startNano = TimeConversions.ToUnixNano(start);
        var endNano = TimeConversions.ToUnixNano(end);
        var where = string.Concat(
            "start_time_unix_nano >= $1 AND start_time_unix_nano < $2",
            " AND service_name IS NOT NULL AND service_name <> ''",
            metric.Predicate is null ? string.Empty : $" AND ({metric.Predicate})",
            serviceName is null ? string.Empty : " AND service_name = $3");

        return store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = string.Create(CultureInfo.InvariantCulture, $"""
                SELECT
                    time_bucket({intervalSql}, to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                    service_name,
                    CAST(({metric.Expression}) AS DOUBLE) AS metric_value
                FROM spans
                WHERE {where}
                GROUP BY bucket, service_name
                ORDER BY service_name ASC, bucket ASC
                """);
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
            if (serviceName is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });

            List<McpMetricServicePoint> points = [];
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                points.Add(new McpMetricServicePoint(
                    reader.GetDateTime(0).ToString("O", CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetDouble(2)));
            }

            return points;
        }, ct);
    }

    internal static Task<List<McpMetricPoint>> QueryGenAiTokenUsagePointsAsync(
        DuckDbStore store,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        string? serviceName,
        string tokenType,
        CancellationToken ct)
    {
        var tokenColumn = tokenType switch
        {
            DerivedMetricCatalog.InputTokenType => "gen_ai_input_tokens",
            DerivedMetricCatalog.OutputTokenType => "gen_ai_output_tokens",
            _ => string.Empty
        };

        if (tokenColumn.Length is 0)
            return Task.FromResult<List<McpMetricPoint>>([]);

        var startNano = TimeConversions.ToUnixNano(start);
        var endNano = TimeConversions.ToUnixNano(end);
        var where = string.Concat(
            "start_time_unix_nano >= $1 AND start_time_unix_nano < $2",
            $" AND {tokenColumn} IS NOT NULL",
            serviceName is null ? string.Empty : " AND service_name = $3");

        return store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = string.Create(CultureInfo.InvariantCulture, $"""
                SELECT
                    time_bucket({intervalSql}, to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                    CAST(COALESCE(SUM({tokenColumn}), 0) AS DOUBLE) AS metric_value
                FROM spans
                WHERE {where}
                GROUP BY bucket
                ORDER BY bucket ASC
                """);
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
            if (serviceName is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });

            List<McpMetricPoint> points = [];
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                points.Add(new McpMetricPoint(
                    reader.GetDateTime(0).ToString("O", CultureInfo.InvariantCulture),
                    reader.GetDouble(1)));
            }

            return points;
        }, ct);
    }

    internal static Task<List<McpMetricServicePoint>> QueryGenAiTokenUsagePointsByServiceAsync(
        DuckDbStore store,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        string? serviceName,
        string tokenType,
        CancellationToken ct)
    {
        var tokenColumn = tokenType switch
        {
            DerivedMetricCatalog.InputTokenType => "gen_ai_input_tokens",
            DerivedMetricCatalog.OutputTokenType => "gen_ai_output_tokens",
            _ => string.Empty
        };

        if (tokenColumn.Length is 0)
            return Task.FromResult<List<McpMetricServicePoint>>([]);

        var startNano = TimeConversions.ToUnixNano(start);
        var endNano = TimeConversions.ToUnixNano(end);
        var where = string.Concat(
            "start_time_unix_nano >= $1 AND start_time_unix_nano < $2",
            " AND service_name IS NOT NULL AND service_name <> ''",
            $" AND {tokenColumn} IS NOT NULL",
            serviceName is null ? string.Empty : " AND service_name = $3");

        return store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = string.Create(CultureInfo.InvariantCulture, $"""
                SELECT
                    time_bucket({intervalSql}, to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket,
                    service_name,
                    CAST(COALESCE(SUM({tokenColumn}), 0) AS DOUBLE) AS metric_value
                FROM spans
                WHERE {where}
                GROUP BY bucket, service_name
                ORDER BY service_name ASC, bucket ASC
                """);
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
            if (serviceName is not null)
                cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });

            List<McpMetricServicePoint> points = [];
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                points.Add(new McpMetricServicePoint(
                    reader.GetDateTime(0).ToString("O", CultureInfo.InvariantCulture),
                    reader.GetString(1),
                    reader.GetDouble(2)));
            }

            return points;
        }, ct);
    }

    internal static bool TryResolveWindow(
        string? from,
        string? to,
        out DateTimeOffset start,
        out DateTimeOffset end,
        out string error)
    {
        var now = TimeProvider.System.GetUtcNow();
        end = now;
        start = now.AddHours(-24);
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(to) &&
            !DateTimeOffset.TryParse(to, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out end))
        {
            error = "Query parameter 'to' must be an ISO-8601 timestamp.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(from) &&
            !DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out start))
        {
            error = "Query parameter 'from' must be an ISO-8601 timestamp.";
            return false;
        }

        if (start >= end)
        {
            error = "Query parameter 'from' must be earlier than 'to'.";
            return false;
        }

        return true;
    }

    internal static bool TryResolveInterval(string? interval, out string intervalSql, out string error)
    {
        intervalSql = "INTERVAL '1 hour'";
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(interval))
            return true;

        intervalSql = interval.Trim().ToLowerInvariant() switch
        {
            "1m" or "1min" or "1 minute" => "INTERVAL '1 minute'",
            "5m" or "5min" or "5 minutes" => "INTERVAL '5 minutes'",
            "15m" or "15min" or "15 minutes" => "INTERVAL '15 minutes'",
            "1h" or "1hr" or "1 hour" => "INTERVAL '1 hour'",
            "1d" or "1 day" => "INTERVAL '1 day'",
            "1w" or "1 week" => "INTERVAL '1 week'",
            _ => string.Empty
        };

        if (intervalSql.Length > 0)
            return true;

        error = "Query parameter 'interval' must be one of 1m, 5m, 15m, 1h, 1d, or 1w.";
        return false;
    }

    internal static bool TryResolveFilter(string? filter, out string? serviceName, out string error)
    {
        serviceName = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var separatorIndex = filter.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == filter.Length - 1)
        {
            error = "Query parameter 'filter' supports service.name=<value> only.";
            return false;
        }

        var key = filter[..separatorIndex].Trim();
        if (!key.Equals(DerivedMetricCatalog.ServiceNameLabel, StringComparison.Ordinal) &&
            !key.Equals("service_name", StringComparison.Ordinal) &&
            !key.Equals("service", StringComparison.Ordinal))
        {
            error = "Query parameter 'filter' supports service.name=<value> only.";
            return false;
        }

        serviceName = filter[(separatorIndex + 1)..].Trim().Trim('"', '\'');
        if (!string.IsNullOrWhiteSpace(serviceName))
            return true;

        error = "Query parameter 'filter' service name cannot be empty.";
        return false;
    }

    internal static bool TryResolveTokenType(
        DerivedMetricDefinition metric,
        string? tokenType,
        out string? resolvedTokenType,
        out string error)
    {
        resolvedTokenType = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(tokenType))
            return true;

        if (!IsGenAiTokenUsageMetric(metric))
        {
            error = "Query parameter 'tokenType' is supported only for gen_ai.client.token.usage.";
            return false;
        }

        resolvedTokenType = tokenType.Trim().ToLowerInvariant() switch
        {
            DerivedMetricCatalog.InputTokenType => DerivedMetricCatalog.InputTokenType,
            DerivedMetricCatalog.OutputTokenType => DerivedMetricCatalog.OutputTokenType,
            _ => null
        };

        if (resolvedTokenType is not null)
            return true;

        error = "Query parameter 'tokenType' must be 'input' or 'output'.";
        return false;
    }

    private static IReadOnlyDictionary<string, string> CreateLabels(string? serviceName, string? tokenType)
    {
        Dictionary<string, string> labels = [];
        if (serviceName is not null)
            labels[DerivedMetricCatalog.ServiceNameLabel] = serviceName;
        if (tokenType is not null)
            labels[DerivedMetricCatalog.GenAiTokenTypeLabel] = tokenType;

        return labels;
    }

    internal static bool IsGenAiTokenUsageMetric(DerivedMetricDefinition metric)
    {
        return metric.Name.Equals(DerivedMetricCatalog.GenAiTokenUsageMetricName, StringComparison.Ordinal);
    }

    internal static bool SupportsGenAiDimensions(DerivedMetricDefinition metric)
    {
        return IsGenAiTokenUsageMetric(metric) ||
               metric.Name.StartsWith("gen_ai.client.", StringComparison.Ordinal);
    }
}

internal sealed record McpMetricInfo(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("unit")]
    string Unit,
    [property: JsonPropertyName("label_keys")]
    IReadOnlyList<string> LabelKeys);

internal sealed record McpMetricSeries(
    [property: JsonPropertyName("metric")]
    string Metric,
    [property: JsonPropertyName("labels")]
    IReadOnlyDictionary<string, string> Labels,
    [property: JsonPropertyName("points")]
    IReadOnlyList<McpMetricPoint> Points);

internal sealed record McpMetricPoint(
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("value")]
    double Value);

internal sealed record McpMetricServicePoint(
    string Timestamp,
    string ServiceName,
    double Value);

internal sealed record McpMetricsError(
    [property: JsonPropertyName("error")]
    string Error);
