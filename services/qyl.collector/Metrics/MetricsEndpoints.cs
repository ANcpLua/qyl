using System.Text.Json.Nodes;
using ANcpLua.Roslyn.Utilities.Time;

namespace Qyl.Collector.Metrics;

internal static class MetricsEndpoints
{
    private const int DefaultLimit = 100;
    private const int MaxLimit = 1000;
    private const int DefaultServiceLimit = 100;
    private const int MaxServiceLimit = 1000;
    private const int DefaultSeriesLimit = 100;
    private const int MaxSeriesLimit = 1000;
    private const int DefaultPointLimit = 10_000;
    private const int MaxPointLimit = 100_000;

    [QylMapEndpoints]
    public static WebApplication MapMetricsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/metrics");

        group.MapGet("", ListMetricsAsync);
        group.MapGet("/{metricName}", GetMetricMetadataAsync);
        group.MapPost("/query", QueryMetricAsync);

        return app;
    }

    private static async Task<IResult> ListMetricsAsync(
        DuckDbStore store,
        string? serviceName,
        string? namePattern,
        int? limit,
        int? serviceLimit,
        string? cursor,
        CancellationToken ct)
    {
        if (!TryResolveCursor(cursor, out var offset, out var cursorError))
            return TypedResults.BadRequest(new PublicMetricsError(cursorError));

        if (!TryResolveLimit(limit, out var pageSize, out var limitError))
            return TypedResults.BadRequest(new PublicMetricsError(limitError));

        if (!TryResolveServiceLimit(serviceLimit, out var resolvedServiceLimit, out var serviceLimitError))
            return TypedResults.BadRequest(new PublicMetricsError(serviceLimitError));

        var metadata = await BuildMetadataAsync(store, serviceName, namePattern, resolvedServiceLimit, ct)
            .ConfigureAwait(false);
        var pageItems = metadata.Skip(offset).Take(pageSize).ToList();
        var hasMore = offset + pageItems.Count < metadata.Count;
        var previousOffset = Math.Max(0, offset - pageSize);

        return TypedResults.Ok(new PublicMetricMetadataPage(
            pageItems,
            hasMore ? (offset + pageItems.Count).ToString(CultureInfo.InvariantCulture) : null,
            offset > 0 ? previousOffset.ToString(CultureInfo.InvariantCulture) : null,
            hasMore));
    }

    private static async Task<IResult> GetMetricMetadataAsync(
        string metricName,
        DuckDbStore store,
        int? serviceLimit,
        CancellationToken ct)
    {
        var metric = McpMetricsEndpoints.FindMetric(metricName);
        if (metric is null)
            return TypedResults.NotFound(new PublicMetricsError($"Unknown metric '{metricName}'."));

        if (!TryResolveServiceLimit(serviceLimit, out var resolvedServiceLimit, out var serviceLimitError))
            return TypedResults.BadRequest(new PublicMetricsError(serviceLimitError));

        var services = await ListServicesForMetricAsync(store, metric, resolvedServiceLimit, preferredServiceName: null, ct)
            .ConfigureAwait(false);
        return TypedResults.Ok(ToPublicMetadata(metric, services, resolvedServiceLimit));
    }

    private static async Task<IResult> QueryMetricAsync(
        PublicMetricQueryRequest? body,
        DuckDbStore store,
        CancellationToken ct)
    {
        if (body is null)
            return TypedResults.BadRequest(new PublicMetricsError("Request body is required."));

        if (string.IsNullOrWhiteSpace(body.MetricName))
            return TypedResults.BadRequest(new PublicMetricsError("Property 'metric_name' is required."));

        var metric = McpMetricsEndpoints.FindMetric(body.MetricName);
        if (metric is null)
            return TypedResults.NotFound(new PublicMetricsError($"Unknown metric '{body.MetricName}'."));

        if (!TryResolveQueryWindow(body, out var start, out var end, out var windowError))
            return TypedResults.BadRequest(new PublicMetricsError(windowError));

        if (!McpMetricsEndpoints.TryResolveInterval(body.Step, out var intervalSql, out var intervalError))
            return TypedResults.BadRequest(new PublicMetricsError(intervalError));

        if (!TryResolveMetricFilters(body.Filters, metric, out var filters, out var filterError))
            return TypedResults.BadRequest(new PublicMetricsError(filterError));

        if (!string.IsNullOrWhiteSpace(body.Aggregation))
            return TypedResults.BadRequest(new PublicMetricsError(
                "Aggregation overrides are not available for derived span metrics; omit 'aggregation' to use the metric default."));

        if (!TryResolveGroupBy(body.GroupBy, metric, out var grouping, out var groupByError))
            return TypedResults.BadRequest(new PublicMetricsError(groupByError));

        if (!TryResolveSeriesLimit(body.SeriesLimit, out var seriesLimit, out var seriesLimitError))
            return TypedResults.BadRequest(new PublicMetricsError(seriesLimitError));

        if (!TryResolvePointLimit(body.PointLimit, out var pointLimit, out var pointLimitError))
            return TypedResults.BadRequest(new PublicMetricsError(pointLimitError));

        var result = await QueryPublicMetricSeriesAsync(
            store,
            metric,
            start,
            end,
            intervalSql,
            filters,
            grouping,
            seriesLimit,
            pointLimit,
            ct).ConfigureAwait(false);

        return TypedResults.Ok(new PublicMetricQueryResponse(
            body.MetricName,
            result.Series,
            result.SeriesTruncated,
            seriesLimit,
            result.PointsTruncated,
            pointLimit));
    }

    private static async Task<List<PublicMetricMetadata>> BuildMetadataAsync(
        DuckDbStore store,
        string? serviceName,
        string? namePattern,
        int serviceLimit,
        CancellationToken ct)
    {
        List<PublicMetricMetadata> metadata = [];
        var resolvedServiceName = string.IsNullOrWhiteSpace(serviceName) ? null : serviceName.Trim();

        foreach (var metric in McpMetricsEndpoints.GetMetricDefinitions())
        {
            if (!MatchesNamePattern(metric.Name, namePattern))
                continue;

            if (resolvedServiceName is not null &&
                !await MetricHasServiceAsync(store, metric, resolvedServiceName, ct).ConfigureAwait(false))
            {
                continue;
            }

            var services = await ListServicesForMetricAsync(store, metric, serviceLimit, resolvedServiceName, ct)
                .ConfigureAwait(false);
            metadata.Add(ToPublicMetadata(metric, services, serviceLimit));
        }

        return metadata;
    }

    private static Task<bool> MetricHasServiceAsync(
        DuckDbStore store,
        DerivedMetricDefinition metric,
        string serviceName,
        CancellationToken ct)
    {
        return store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = string.Concat(
                "SELECT 1 FROM spans WHERE service_name = $1",
                metric.Predicate is null ? string.Empty : $" AND ({metric.Predicate})",
                " LIMIT 1");
            cmd.Parameters.Add(new DuckDBParameter { Value = serviceName });
            var result = cmd.ExecuteScalar();
            return result is not null;
        }, ct);
    }

    private static Task<MetricServiceList> ListServicesForMetricAsync(
        DuckDbStore store,
        DerivedMetricDefinition metric,
        int serviceLimit,
        string? preferredServiceName,
        CancellationToken ct)
    {
        return store.ExecuteReadAsync(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = string.Concat(
                "SELECT DISTINCT service_name FROM spans WHERE service_name IS NOT NULL AND service_name <> ''",
                metric.Predicate is null ? string.Empty : $" AND ({metric.Predicate})",
                " ORDER BY service_name ASC LIMIT ",
                (serviceLimit + 1).ToString(CultureInfo.InvariantCulture));

            List<string> services = [];
            var servicesTruncated = false;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (services.Count < serviceLimit)
                {
                    services.Add(reader.GetString(0));
                    continue;
                }

                servicesTruncated = true;
                break;
            }

            if (preferredServiceName is not null)
                return PrioritizePreferredService(services, servicesTruncated, preferredServiceName, serviceLimit);

            return new MetricServiceList(services, servicesTruncated);
        }, ct);
    }

    private static MetricServiceList PrioritizePreferredService(
        List<string> services,
        bool servicesTruncated,
        string preferredServiceName,
        int serviceLimit)
    {
        var preferredIndex = services.FindIndex(service =>
            string.Equals(service, preferredServiceName, StringComparison.Ordinal));
        if (preferredIndex is 0)
            return new MetricServiceList(services, servicesTruncated);

        if (preferredIndex > 0)
        {
            services.RemoveAt(preferredIndex);
            services.Insert(0, preferredServiceName);
            return new MetricServiceList(services, servicesTruncated);
        }

        services.Insert(0, preferredServiceName);
        if (services.Count > serviceLimit)
        {
            services.RemoveAt(services.Count - 1);
            servicesTruncated = true;
        }

        return new MetricServiceList(services, servicesTruncated);
    }

    private static PublicMetricMetadata ToPublicMetadata(
        DerivedMetricDefinition metric,
        MetricServiceList services,
        int serviceLimit)
    {
        return new PublicMetricMetadata(
            metric.Name,
            metric.Description,
            metric.Unit,
            metric.Type,
            McpMetricsEndpoints.GetLabelKeys(metric),
            services.Services,
            services.ServicesTruncated,
            serviceLimit);
    }

    private static bool MatchesNamePattern(string name, string? namePattern)
    {
        if (string.IsNullOrWhiteSpace(namePattern))
            return true;

        var pattern = namePattern.Trim().Trim('*');
        return pattern.Length is 0 || name.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveLimit(
        int? limit,
        out int pageSize,
        out string error)
    {
        pageSize = limit ?? DefaultLimit;
        error = string.Empty;

        if (pageSize is >= 1 and <= MaxLimit)
            return true;

        error = $"Query parameter 'limit' must be between 1 and {MaxLimit.ToString(CultureInfo.InvariantCulture)}.";
        return false;
    }

    private static bool TryResolveServiceLimit(
        int? serviceLimit,
        out int limit,
        out string error)
    {
        limit = serviceLimit ?? DefaultServiceLimit;
        error = string.Empty;

        if (limit is >= 1 and <= MaxServiceLimit)
            return true;

        error =
            $"Query parameter 'serviceLimit' must be between 1 and {MaxServiceLimit.ToString(CultureInfo.InvariantCulture)}.";
        return false;
    }

    private static bool TryResolveCursor(
        string? cursor,
        out int offset,
        out string error)
    {
        offset = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        if (int.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset >= 0)
            return true;

        error = "Query parameter 'cursor' must be a non-negative integer offset.";
        return false;
    }

    private static bool TryResolveQueryWindow(
        PublicMetricQueryRequest body,
        out DateTimeOffset start,
        out DateTimeOffset end,
        out string error)
    {
        start = default;
        end = default;
        error = string.Empty;

        if (body.StartTime is not { } startTime)
        {
            error = "Property 'start_time' is required.";
            return false;
        }

        if (body.EndTime is not { } endTime)
        {
            error = "Property 'end_time' is required.";
            return false;
        }

        start = startTime;
        end = endTime;

        if (start < end)
            return true;

        error = "Property 'start_time' must be earlier than 'end_time'.";
        return false;
    }

    private static bool TryResolveSeriesLimit(
        int? seriesLimit,
        out int limit,
        out string error)
    {
        limit = seriesLimit ?? DefaultSeriesLimit;
        error = string.Empty;

        if (limit is >= 1 and <= MaxSeriesLimit)
            return true;

        error = $"Property 'series_limit' must be between 1 and {MaxSeriesLimit.ToString(CultureInfo.InvariantCulture)}.";
        return false;
    }

    private static bool TryResolvePointLimit(
        int? pointLimit,
        out int limit,
        out string error)
    {
        limit = pointLimit ?? DefaultPointLimit;
        error = string.Empty;

        if (limit is >= 1 and <= MaxPointLimit)
            return true;

        error = $"Property 'point_limit' must be between 1 and {MaxPointLimit.ToString(CultureInfo.InvariantCulture)}.";
        return false;
    }

    private static bool TryResolveMetricFilters(
        JsonObject? filters,
        DerivedMetricDefinition metric,
        out MetricFilterSelection selection,
        out string error)
    {
        string? serviceName = null;
        string? tokenType = null;
        string? providerName = null;
        string? requestModel = null;
        var hasServiceNameFilter = false;
        var hasTokenTypeFilter = false;
        var hasProviderNameFilter = false;
        var hasRequestModelFilter = false;
        selection = new MetricFilterSelection(null, null, null, null);
        error = string.Empty;

        if (filters is null || filters.Count is 0)
            return true;

        foreach (var filter in filters)
        {
            if (IsServiceNameFilter(filter.Key))
            {
                if (hasServiceNameFilter)
                {
                    error = "Metric filters specify service.name more than once.";
                    return false;
                }

                if (!TryReadRequiredStringFilter(filter, DerivedMetricCatalog.ServiceNameLabel, out serviceName, out error))
                    return false;

                hasServiceNameFilter = true;
                continue;
            }

            if (filter.Key.Equals(DerivedMetricCatalog.GenAiTokenTypeLabel, StringComparison.Ordinal))
            {
                if (hasTokenTypeFilter)
                {
                    error = "Metric filters specify gen_ai.token.type more than once.";
                    return false;
                }

                if (!IsGenAiTokenUsageMetric(metric))
                {
                    error = "Metric filters support gen_ai.token.type only for gen_ai.client.token.usage.";
                    return false;
                }

                if (!TryReadRequiredStringFilter(filter, DerivedMetricCatalog.GenAiTokenTypeLabel, out tokenType, out error))
                    return false;

                var normalizedTokenType = NormalizeTokenType(tokenType);
                if (normalizedTokenType is not null)
                {
                    tokenType = normalizedTokenType;
                    hasTokenTypeFilter = true;
                    continue;
                }

                error = "Metric filter gen_ai.token.type must be 'input' or 'output'.";
                return false;
            }

            if (filter.Key.Equals(DerivedMetricCatalog.GenAiProviderNameLabel, StringComparison.Ordinal))
            {
                if (hasProviderNameFilter)
                {
                    error = "Metric filters specify gen_ai.provider.name more than once.";
                    return false;
                }

                if (!McpMetricsEndpoints.SupportsGenAiDimensions(metric))
                {
                    error = "Metric filters support gen_ai.provider.name only for GenAI-derived metrics.";
                    return false;
                }

                if (!TryReadRequiredStringFilter(filter, DerivedMetricCatalog.GenAiProviderNameLabel, out providerName,
                        out error))
                    return false;

                hasProviderNameFilter = true;
                continue;
            }

            if (filter.Key.Equals(DerivedMetricCatalog.GenAiRequestModelLabel, StringComparison.Ordinal))
            {
                if (hasRequestModelFilter)
                {
                    error = "Metric filters specify gen_ai.request.model more than once.";
                    return false;
                }

                if (!McpMetricsEndpoints.SupportsGenAiDimensions(metric))
                {
                    error = "Metric filters support gen_ai.request.model only for GenAI-derived metrics.";
                    return false;
                }

                if (!TryReadRequiredStringFilter(filter, DerivedMetricCatalog.GenAiRequestModelLabel, out requestModel,
                        out error))
                    return false;

                hasRequestModelFilter = true;
                continue;
            }

            error = IsGenAiTokenUsageMetric(metric)
                ? "Metric filters support service.name, gen_ai.provider.name, gen_ai.request.model, and gen_ai.token.type only."
                : McpMetricsEndpoints.SupportsGenAiDimensions(metric)
                    ? "Metric filters support service.name, gen_ai.provider.name, and gen_ai.request.model only."
                    : "Metric filters support service.name only.";
            return false;
        }

        selection = new MetricFilterSelection(serviceName, tokenType, providerName, requestModel);
        return true;
    }

    private static bool TryResolveGroupBy(
        IReadOnlyList<string>? groupBy,
        DerivedMetricDefinition metric,
        out MetricGroupingSelection selection,
        out string error)
    {
        selection = new MetricGroupingSelection(false, false, false, false);
        error = string.Empty;

        if (groupBy is null || groupBy.Count is 0)
            return true;

        var groupByServiceName = false;
        var groupByTokenType = false;
        var groupByProviderName = false;
        var groupByRequestModel = false;

        foreach (var label in groupBy)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                error = "Grouping labels must be non-empty strings.";
                return false;
            }

            if (label.Equals(DerivedMetricCatalog.ServiceNameLabel, StringComparison.Ordinal))
            {
                if (groupByServiceName)
                {
                    error = "Grouping specifies service.name more than once.";
                    return false;
                }

                groupByServiceName = true;
                continue;
            }

            if (label.Equals(DerivedMetricCatalog.GenAiTokenTypeLabel, StringComparison.Ordinal))
            {
                if (!IsGenAiTokenUsageMetric(metric))
                {
                    error = "Grouping supports gen_ai.token.type only for gen_ai.client.token.usage.";
                    return false;
                }

                if (groupByTokenType)
                {
                    error = "Grouping specifies gen_ai.token.type more than once.";
                    return false;
                }

                groupByTokenType = true;
                continue;
            }

            if (label.Equals(DerivedMetricCatalog.GenAiProviderNameLabel, StringComparison.Ordinal))
            {
                if (!McpMetricsEndpoints.SupportsGenAiDimensions(metric))
                {
                    error = "Grouping supports gen_ai.provider.name only for GenAI-derived metrics.";
                    return false;
                }

                if (groupByProviderName)
                {
                    error = "Grouping specifies gen_ai.provider.name more than once.";
                    return false;
                }

                groupByProviderName = true;
                continue;
            }

            if (label.Equals(DerivedMetricCatalog.GenAiRequestModelLabel, StringComparison.Ordinal))
            {
                if (!McpMetricsEndpoints.SupportsGenAiDimensions(metric))
                {
                    error = "Grouping supports gen_ai.request.model only for GenAI-derived metrics.";
                    return false;
                }

                if (groupByRequestModel)
                {
                    error = "Grouping specifies gen_ai.request.model more than once.";
                    return false;
                }

                groupByRequestModel = true;
                continue;
            }

            error = IsGenAiTokenUsageMetric(metric)
                ? "Grouping supports service.name, gen_ai.provider.name, gen_ai.request.model, and gen_ai.token.type only for gen_ai.client.token.usage."
                : McpMetricsEndpoints.SupportsGenAiDimensions(metric)
                    ? "Grouping supports service.name, gen_ai.provider.name, and gen_ai.request.model only for GenAI-derived metrics."
                    : "Grouping supports service.name only for derived span metrics.";
            return false;
        }

        selection = new MetricGroupingSelection(
            groupByServiceName,
            groupByTokenType,
            groupByProviderName,
            groupByRequestModel);
        return true;
    }

    private static async Task<PublicMetricQueryResult> QueryPublicMetricSeriesAsync(
        DuckDbStore store,
        DerivedMetricDefinition metric,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        MetricFilterSelection filters,
        MetricGroupingSelection grouping,
        int seriesLimit,
        int pointLimit,
        CancellationToken ct)
    {
        var limits = new MetricQueryLimits(seriesLimit, pointLimit);

        if (IsGenAiTokenUsageMetric(metric) && (grouping.GroupByTokenType || filters.TokenType is not null))
            return await QueryGenAiTokenUsageSeriesAsync(
                store,
                start,
                end,
                intervalSql,
                filters,
                grouping,
                limits,
                ct).ConfigureAwait(false);

        var series = await QuerySqlSeriesAsync(
            store,
            metric.Expression,
            metric.Predicate,
            start,
            end,
            intervalSql,
            filters,
            grouping,
            tokenType: null,
            limits: limits,
            ct).ConfigureAwait(false);
        return new PublicMetricQueryResult(series, limits.SeriesLimitExceeded, limits.PointLimitExceeded);
    }

    private static async Task<PublicMetricQueryResult> QueryGenAiTokenUsageSeriesAsync(
        DuckDbStore store,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        MetricFilterSelection filters,
        MetricGroupingSelection grouping,
        MetricQueryLimits limits,
        CancellationToken ct)
    {
        IReadOnlyList<string> tokenTypes = filters.TokenType is { } selectedTokenType
            ? [selectedTokenType]
            : [DerivedMetricCatalog.InputTokenType, DerivedMetricCatalog.OutputTokenType];

        List<PublicMetricTimeSeries> series = [];
        foreach (var tokenType in tokenTypes)
        {
            var tokenColumn = tokenType switch
            {
                DerivedMetricCatalog.InputTokenType => "gen_ai_input_tokens",
                DerivedMetricCatalog.OutputTokenType => "gen_ai_output_tokens",
                _ => string.Empty
            };

            if (tokenColumn.Length is 0)
                continue;

            var tokenSeries = await QuerySqlSeriesAsync(
                store,
                $"COALESCE(SUM({tokenColumn}), 0)",
                $"{tokenColumn} IS NOT NULL",
                start,
                end,
                intervalSql,
                filters,
                grouping,
                tokenType,
                limits,
                ct).ConfigureAwait(false);

            series.AddRange(tokenSeries);
            if (limits.SeriesLimitExceeded || limits.PointLimitExceeded)
                break;
        }

        return new PublicMetricQueryResult(series, limits.SeriesLimitExceeded, limits.PointLimitExceeded);
    }

    private static async Task<IReadOnlyList<PublicMetricTimeSeries>> QuerySqlSeriesAsync(
        DuckDbStore store,
        string expression,
        string? predicate,
        DateTimeOffset start,
        DateTimeOffset end,
        string intervalSql,
        MetricFilterSelection filters,
        MetricGroupingSelection grouping,
        string? tokenType,
        MetricQueryLimits limits,
        CancellationToken ct)
    {
        var groupedLabels = GetGroupedLabelColumns(grouping);
        var startNano = TimeConversions.ToUnixNano(start);
        var endNano = TimeConversions.ToUnixNano(end);
        var where = BuildMetricWhereClause(predicate, filters, groupedLabels);
        var sql = BuildMetricQuerySql(expression, intervalSql, where, groupedLabels, limits.SqlRowLimit);

        return await store.ExecuteReadAsync<IReadOnlyList<PublicMetricTimeSeries>>(con =>
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.Add(new DuckDBParameter { Value = startNano });
            cmd.Parameters.Add(new DuckDBParameter { Value = endNano });
            AddFilterParameters(cmd, filters);

            var seriesByKey = new Dictionary<string, PublicMetricSeriesBuilder>(StringComparer.Ordinal);
            var valueOrdinal = groupedLabels.Count + 1;
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var labels = CreateLabels(filters.ServiceName, tokenType, filters.ProviderName, filters.RequestModel);
                for (var i = 0; i < groupedLabels.Count; i++)
                    labels[groupedLabels[i].Label] = reader.GetString(i + 1);

                var key = CreateSeriesKey(labels);
                if (!seriesByKey.TryGetValue(key, out var series))
                {
                    if (!limits.TryAcceptSeries())
                        break;

                    if (!limits.TryAcceptPoint())
                        break;

                    series = new PublicMetricSeriesBuilder(labels);
                    seriesByKey.Add(key, series);
                }
                else if (!limits.TryAcceptPoint())
                {
                    break;
                }

                series.Points.Add(new PublicMetricDataPoint(
                    reader.GetDateTime(0).ToString("O", CultureInfo.InvariantCulture),
                    reader.GetDouble(valueOrdinal)));
            }

            List<PublicMetricTimeSeries> result = [];
            foreach (var series in seriesByKey.Values)
                result.Add(new PublicMetricTimeSeries(series.Labels, series.Points));

            return result;
        }, ct).ConfigureAwait(false);
    }

    private static string BuildMetricWhereClause(
        string? predicate,
        MetricFilterSelection filters,
        IReadOnlyList<MetricLabelColumn> groupedLabels)
    {
        var where = new StringBuilder("start_time_unix_nano >= $1 AND start_time_unix_nano < $2");
        if (predicate is not null)
            where.Append(" AND (").Append(predicate).Append(')');

        foreach (var label in groupedLabels)
            where.Append(" AND ").Append(label.Column).Append(" IS NOT NULL AND ").Append(label.Column)
                .Append(" <> ''");

        var parameterIndex = 3;
        AppendFilterCondition(where, "service_name", filters.ServiceName, ref parameterIndex);
        AppendFilterCondition(where, "gen_ai_provider_name", filters.ProviderName, ref parameterIndex);
        AppendFilterCondition(where, "gen_ai_request_model", filters.RequestModel, ref parameterIndex);
        return where.ToString();
    }

    internal static string BuildMetricQuerySql(
        string expression,
        string intervalSql,
        string where,
        IReadOnlyList<MetricLabelColumn> groupedLabels,
        int rowLimit)
    {
        var sql = new StringBuilder();
        sql.Append("SELECT time_bucket(").Append(intervalSql)
            .Append(", to_timestamp(start_time_unix_nano / 1000000000.0)) AS bucket");

        foreach (var label in groupedLabels)
            sql.Append(", ").Append(label.Column);

        sql.Append(", CAST((").Append(expression).Append(") AS DOUBLE) AS metric_value FROM spans WHERE ")
            .Append(where)
            .Append(" GROUP BY bucket");

        foreach (var label in groupedLabels)
            sql.Append(", ").Append(label.Column);

        sql.Append(" ORDER BY ");
        for (var i = 0; i < groupedLabels.Count; i++)
        {
            if (i > 0)
                sql.Append(", ");

            sql.Append(groupedLabels[i].Column).Append(" ASC");
        }

        if (groupedLabels.Count > 0)
            sql.Append(", ");

        sql.Append("bucket ASC LIMIT ")
            .Append(rowLimit.ToString(CultureInfo.InvariantCulture));
        return sql.ToString();
    }

    private static void AppendFilterCondition(
        StringBuilder where,
        string columnName,
        string? value,
        ref int parameterIndex)
    {
        if (value is null)
            return;

        where.Append(" AND ").Append(columnName).Append(" = $")
            .Append(parameterIndex.ToString(CultureInfo.InvariantCulture));
        parameterIndex++;
    }

    private static void AddFilterParameters(DuckDBCommand cmd, MetricFilterSelection filters)
    {
        AddFilterParameter(cmd, filters.ServiceName);
        AddFilterParameter(cmd, filters.ProviderName);
        AddFilterParameter(cmd, filters.RequestModel);
    }

    private static void AddFilterParameter(DuckDBCommand cmd, string? value)
    {
        if (value is not null)
            cmd.Parameters.Add(new DuckDBParameter { Value = value });
    }

    private static IReadOnlyList<MetricLabelColumn> GetGroupedLabelColumns(MetricGroupingSelection grouping)
    {
        List<MetricLabelColumn> labels = [];
        if (grouping.GroupByServiceName)
            labels.Add(new MetricLabelColumn(DerivedMetricCatalog.ServiceNameLabel, "service_name"));
        if (grouping.GroupByProviderName)
            labels.Add(new MetricLabelColumn(DerivedMetricCatalog.GenAiProviderNameLabel, "gen_ai_provider_name"));
        if (grouping.GroupByRequestModel)
            labels.Add(new MetricLabelColumn(DerivedMetricCatalog.GenAiRequestModelLabel, "gen_ai_request_model"));
        return labels;
    }

    private static JsonObject CreateLabels(
        string? serviceName,
        string? tokenType,
        string? providerName,
        string? requestModel)
    {
        var labels = new JsonObject();
        if (serviceName is not null)
            labels[DerivedMetricCatalog.ServiceNameLabel] = serviceName;
        if (providerName is not null)
            labels[DerivedMetricCatalog.GenAiProviderNameLabel] = providerName;
        if (requestModel is not null)
            labels[DerivedMetricCatalog.GenAiRequestModelLabel] = requestModel;
        if (tokenType is not null)
            labels[DerivedMetricCatalog.GenAiTokenTypeLabel] = tokenType;
        return labels;
    }

    private static string CreateSeriesKey(JsonObject labels)
    {
        var key = new StringBuilder();
        foreach (var label in labels.OrderBy(static label => label.Key, StringComparer.Ordinal))
        {
            key.Append(label.Key).Append('=');
            if (label.Value is not null)
                key.Append(label.Value.GetValue<string>());

            key.Append(';');
        }

        return key.ToString();
    }

    private static bool TryReadRequiredStringFilter(
        KeyValuePair<string, JsonNode?> filter,
        string label,
        out string value,
        out string error)
    {
        value = filter.Value?.GetValueKind() is JsonValueKind.String
            ? filter.Value.GetValue<string>().Trim()
            : string.Empty;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(value))
            return true;

        error = $"Metric filter {label} must be a non-empty string.";
        return false;
    }

    private static string? NormalizeTokenType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            DerivedMetricCatalog.InputTokenType => DerivedMetricCatalog.InputTokenType,
            DerivedMetricCatalog.OutputTokenType => DerivedMetricCatalog.OutputTokenType,
            _ => null
        };
    }

    private static bool IsServiceNameFilter(string key)
    {
        return key.Equals(DerivedMetricCatalog.ServiceNameLabel, StringComparison.Ordinal) ||
               key.Equals("service_name", StringComparison.Ordinal) ||
               key.Equals("service", StringComparison.Ordinal);
    }

    private static bool IsGenAiTokenUsageMetric(DerivedMetricDefinition metric)
    {
        return McpMetricsEndpoints.IsGenAiTokenUsageMetric(metric);
    }
}

internal sealed record PublicMetricMetadata(
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("description")]
    string Description,
    [property: JsonPropertyName("unit")]
    string Unit,
    [property: JsonPropertyName("type")]
    string Type,
    [property: JsonPropertyName("label_keys")]
    IReadOnlyList<string> LabelKeys,
    [property: JsonPropertyName("services")]
    IReadOnlyList<string> Services,
    [property: JsonPropertyName("services_truncated")]
    bool ServicesTruncated,
    [property: JsonPropertyName("service_limit")]
    int ServiceLimit);

internal sealed record PublicMetricMetadataPage(
    [property: JsonPropertyName("items")]
    IReadOnlyList<PublicMetricMetadata> Items,
    [property: JsonPropertyName("next_cursor")]
    string? NextCursor,
    [property: JsonPropertyName("prev_cursor")]
    string? PrevCursor,
    [property: JsonPropertyName("has_more")]
    bool HasMore);

internal sealed class PublicMetricQueryRequest
{
    [JsonPropertyName("metric_name")]
    public string? MetricName { get; init; }

    [JsonPropertyName("filters")]
    public JsonObject? Filters { get; init; }

    [JsonPropertyName("start_time")]
    public DateTimeOffset? StartTime { get; init; }

    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; init; }

    [JsonPropertyName("step")]
    public string? Step { get; init; }

    [JsonPropertyName("aggregation")]
    public string? Aggregation { get; init; }

    [JsonPropertyName("group_by")]
    public IReadOnlyList<string>? GroupBy { get; init; }

    [JsonPropertyName("series_limit")]
    public int? SeriesLimit { get; init; }

    [JsonPropertyName("point_limit")]
    public int? PointLimit { get; init; }
}

internal sealed record PublicMetricQueryResponse(
    [property: JsonPropertyName("metric_name")]
    string MetricName,
    [property: JsonPropertyName("series")]
    IReadOnlyList<PublicMetricTimeSeries> Series,
    [property: JsonPropertyName("series_truncated")]
    bool SeriesTruncated,
    [property: JsonPropertyName("series_limit")]
    int SeriesLimit,
    [property: JsonPropertyName("points_truncated")]
    bool PointsTruncated,
    [property: JsonPropertyName("point_limit")]
    int PointLimit);

internal sealed record PublicMetricTimeSeries(
    [property: JsonPropertyName("labels")]
    JsonObject Labels,
    [property: JsonPropertyName("points")]
    IEnumerable<PublicMetricDataPoint> Points);

internal sealed record PublicMetricDataPoint(
    [property: JsonPropertyName("timestamp")]
    string Timestamp,
    [property: JsonPropertyName("value")]
    double Value);

internal sealed record PublicMetricsError(
    [property: JsonPropertyName("error")]
    string Error);

internal sealed record MetricFilterSelection(
    string? ServiceName,
    string? TokenType,
    string? ProviderName,
    string? RequestModel);

internal sealed record MetricServiceList(
    IReadOnlyList<string> Services,
    bool ServicesTruncated);

internal sealed record MetricGroupingSelection(
    bool GroupByServiceName,
    bool GroupByTokenType,
    bool GroupByProviderName,
    bool GroupByRequestModel);

internal sealed record MetricLabelColumn(string Label, string Column);

internal sealed record PublicMetricQueryResult(
    IReadOnlyList<PublicMetricTimeSeries> Series,
    bool SeriesTruncated,
    bool PointsTruncated);

internal sealed class MetricQueryLimits(int seriesLimit, int pointLimit)
{
    private int _acceptedSeries;
    private int _acceptedPoints;

    public bool SeriesLimitExceeded { get; private set; }

    public bool PointLimitExceeded { get; private set; }

    public int SqlRowLimit
    {
        get
        {
            var remaining = pointLimit - _acceptedPoints;
            return remaining > 0 ? remaining + 1 : 1;
        }
    }

    public bool TryAcceptSeries()
    {
        if (_acceptedSeries < seriesLimit)
        {
            _acceptedSeries++;
            return true;
        }

        SeriesLimitExceeded = true;
        return false;
    }

    public bool TryAcceptPoint()
    {
        if (_acceptedPoints < pointLimit)
        {
            _acceptedPoints++;
            return true;
        }

        PointLimitExceeded = true;
        return false;
    }
}

internal sealed record PublicMetricSeriesBuilder(
    JsonObject Labels,
    List<PublicMetricDataPoint> Points)
{
    public PublicMetricSeriesBuilder(JsonObject labels)
        : this(labels, [])
    {
    }
}
