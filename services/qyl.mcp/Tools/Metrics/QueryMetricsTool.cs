using System.Net;
using System.Net.Http.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Formatting;
using qyl.mcp.Tools;

namespace qyl.mcp.Tools.Metrics;

[McpServerToolType]
[QylSkill(QylSkillKind.Inspect)]
public sealed partial class QueryMetricsTool(HttpClient client, TimeProvider timeProvider)
{
    private const string TokenTypeLabel = "gen_ai.token.type";
    private const string ProviderNameLabel = "gen_ai.provider.name";
    private const string RequestModelLabel = "gen_ai.request.model";

    public QueryMetricsTool(HttpClient client)
        : this(client, TimeProvider.System)
    {
    }

    [McpServerTool(
        Name = "query_metrics",
        Title = "Query Metrics",
        ReadOnly = true,
        Destructive = false,
        OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public async partial Task<string> QueryMetrics(
        string name,
        string? filter = null,
        string? from = null,
        string? to = null,
        string? interval = null,
        string? tokenType = null,
        string? groupBy = null,
        string? providerName = null,
        string? requestModel = null,
        int? seriesLimit = null,
        int? pointLimit = null,
        CancellationToken ct = default)
    {
        return await QueryPublicMetricsAsync(
                name,
                filter,
                from,
                to,
                interval,
                tokenType,
                groupBy,
                providerName,
                requestModel,
                seriesLimit,
                pointLimit,
                ct)
            .ConfigureAwait(false);
    }

    private async Task<string> QueryPublicMetricsAsync(
        string name,
        string? filter,
        string? from,
        string? to,
        string? interval,
        string? tokenType,
        string? groupBy,
        string? providerName,
        string? requestModel,
        int? seriesLimit,
        int? pointLimit,
        CancellationToken ct)
    {
        if (!TryCreatePublicQueryFilters(
                filter,
                tokenType,
                providerName,
                requestModel,
                out var filters,
                out var filterError))
        {
            return $"Metric query rejected: {filterError}";
        }

        var groupByLabels = string.IsNullOrWhiteSpace(groupBy)
            ? null
            : SplitCommaSeparated(groupBy);
        if (groupBy is not null && groupByLabels is { Count: 0 })
            return "Metric query rejected: Query parameter 'groupBy' must include at least one label.";

        var (startTime, endTime) = ResolvePublicMetricWindow(from, to);
        var request = new PublicMetricQueryRequestDto(
            name,
            filters,
            startTime,
            endTime,
            interval,
            groupByLabels,
            seriesLimit,
            pointLimit);

        using var response = await client.PostAsJsonAsync(
                "/api/v1/metrics/query",
                request,
                CollectorDtosJsonContext.Default.PublicMetricQueryRequestDto,
                ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return await FormatQueryFailureAsync(name, response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync(
                CollectorDtosJsonContext.Default.PublicMetricQueryResponseDto,
                ct)
            .ConfigureAwait(false);

        if (result is null || !HasGroupedPoints(result))
            return $"No data points found for metric `{name}`.";

        return FormatGroupedMetric(result);
    }

    private (string StartTime, string EndTime) ResolvePublicMetricWindow(string? from, string? to)
    {
        var now = timeProvider.GetUtcNow();
        var endTime = string.IsNullOrWhiteSpace(to)
            ? now.ToString("O", CultureInfo.InvariantCulture)
            : to;

        var startTime = string.IsNullOrWhiteSpace(from)
            ? ResolveDefaultStartTime(to, now)
            : from;

        return (startTime, endTime);
    }

    private static string ResolveDefaultStartTime(string? to, DateTimeOffset now)
    {
        var endTime = DateTimeOffset.TryParse(
            to,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsedEnd)
            ? parsedEnd
            : now;

        return endTime.AddHours(-24).ToString("O", CultureInfo.InvariantCulture);
    }

    private static string FormatGroupedMetric(PublicMetricQueryResponseDto result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Metric: `{result.MetricName}`");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Series:** {result.Series.Count}");
        if (result.SeriesTruncated)
        {
            var seriesLimit = result.SeriesLimit?.ToString(CultureInfo.InvariantCulture) ?? "collector limit";
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Series limit:** {seriesLimit} (truncated)");
        }
        if (result.PointsTruncated)
        {
            var pointLimit = result.PointLimit?.ToString(CultureInfo.InvariantCulture) ?? "collector limit";
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Point limit:** {pointLimit} (truncated)");
        }

        foreach (var series in result.Series)
        {
            sb.AppendLine();
            sb.Append("## Series");
            if (series.Labels is { Count: > 0 })
            {
                sb.Append(": ");
                sb.Append(FormatLabels(series.Labels));
            }

            sb.AppendLine();
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Points:** {series.Points.Count}");
            sb.AppendLine();
            sb.AppendLine("| Timestamp | Value |");
            sb.AppendLine("|-----------|-------|");

            foreach (var point in series.Points)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"| {point.Timestamp} | {point.Value:G6} |");
            }
        }

        return sb.ToString();
    }

    private static bool HasGroupedPoints(PublicMetricQueryResponseDto result)
    {
        if (result.Series.Count is 0)
            return false;

        foreach (var series in result.Series)
        {
            if (series.Points.Count > 0)
                return true;
        }

        return false;
    }

    private static IReadOnlyList<string> SplitCommaSeparated(string value)
    {
        List<string> items = [];
        var start = 0;

        for (var index = 0; index <= value.Length; index++)
        {
            if (index < value.Length && value[index] != ',')
                continue;

            var item = value[start..index].Trim();
            if (item.Length > 0)
                items.Add(item);

            start = index + 1;
        }

        return items;
    }

    private static bool TryCreatePublicQueryFilters(
        string? filter,
        string? tokenType,
        string? providerName,
        string? requestModel,
        out Dictionary<string, string>? filters,
        out string error)
    {
        filters = null;
        error = string.Empty;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var separatorIndex = filter.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == filter.Length - 1)
            {
                error = "Query parameter 'filter' must be a single key=value label filter.";
                return false;
            }

            var key = filter[..separatorIndex].Trim();
            var value = filter[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            if (key.Length is 0 || value.Length is 0)
            {
                error = "Query parameter 'filter' must be a single key=value label filter.";
                return false;
            }

            filters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [key] = value
            };
        }

        if (!TryAddNamedFilter(ref filters, TokenTypeLabel, tokenType, nameof(tokenType), out error))
            return false;
        if (!TryAddNamedFilter(ref filters, ProviderNameLabel, providerName, nameof(providerName), out error))
            return false;
        if (!TryAddNamedFilter(ref filters, RequestModelLabel, requestModel, nameof(requestModel), out error))
            return false;

        return true;
    }

    private static bool TryAddNamedFilter(
        ref Dictionary<string, string>? filters,
        string label,
        string? value,
        string parameterName,
        out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        filters ??= new Dictionary<string, string>(StringComparer.Ordinal);
        if (filters.ContainsKey(label))
        {
            error = $"Query parameter '{parameterName}' duplicates filter label {label}.";
            return false;
        }

        filters[label] = value.Trim();
        return true;
    }

    private static string FormatLabels(IReadOnlyDictionary<string, string> labels)
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var (key, value) in labels)
        {
            if (!first)
                sb.Append(", ");

            sb.Append('`');
            sb.Append(key);
            sb.Append('=');
            sb.Append(value);
            sb.Append('`');
            first = false;
        }

        return sb.ToString();
    }

    private static async Task<string> FormatQueryFailureAsync(
        string metricName,
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var message = await CollectorHelper.ReadCollectorErrorMessageAsync(response, ct).ConfigureAwait(false);
        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => $"Metric `{metricName}` was not found. {message}",
            HttpStatusCode.BadRequest => $"Metric query rejected: {message}",
            _ => $"Metric query failed ({(int)response.StatusCode} {response.StatusCode}): {message}"
        };
    }
}
