// =============================================================================
// qyl.copilot - Telemetry Search Provider
// RAG-mode search backed by qyl collector telemetry data
// Uses Microsoft.Agents.AI TextSearchProvider pattern
// =============================================================================

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Agents.AI;
using qyl.copilot.Instrumentation;

namespace qyl.copilot.Adapters;

/// <summary>
///     RAG-mode search provider that queries qyl collector telemetry data.
///     Useful for "what happened in my last deployment?" type queries.
///     Implements the <see cref="TextSearchProvider"/> search pattern.
/// </summary>
public sealed class QylSearchProvider : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _collectorBaseUrl;
    private bool _disposed;

    /// <summary>
    ///     Creates a new telemetry search provider.
    /// </summary>
    /// <param name="httpClient">HTTP client for calling the collector API.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="collectorBaseUrl">Base URL of the qyl collector (default: http://localhost:5100).</param>
    public QylSearchProvider(
        HttpClient httpClient,
        ILogger<QylSearchProvider> logger,
        string collectorBaseUrl = "http://localhost:5100")
    {
        _httpClient = Guard.NotNull(httpClient);
        _logger = Guard.NotNull(logger);
        _collectorBaseUrl = collectorBaseUrl.TrimEnd('/');
    }

    /// <summary>
    ///     Searches spans matching the query text.
    /// </summary>
    /// <param name="query">Search query (matched against span names, attributes, and status).</param>
    /// <param name="maxResults">Maximum results to return (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results formatted as agent-consumable context.</returns>
    public async Task<IReadOnlyList<TelemetrySearchResult>> SearchSpansAsync(
        string query,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        using var activity = CopilotInstrumentation.ActivitySource.StartActivity("qyl.search.spans");
        activity?.SetTag("qyl.search.query", query);

        try
        {
            var url = $"{_collectorBaseUrl}/api/v1/spans?query={Uri.EscapeDataString(query)}&limit={maxResults}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Span search failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var spans = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
            return ParseSpanResults(spans);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Span search failed for query: {Query}", query);
            return [];
        }
    }

    /// <summary>
    ///     Searches logs matching the query text.
    /// </summary>
    /// <param name="query">Search query (matched against log body and attributes).</param>
    /// <param name="maxResults">Maximum results to return (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results formatted as agent-consumable context.</returns>
    public async Task<IReadOnlyList<TelemetrySearchResult>> SearchLogsAsync(
        string query,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        using var activity = CopilotInstrumentation.ActivitySource.StartActivity("qyl.search.logs");
        activity?.SetTag("qyl.search.query", query);

        try
        {
            var url = $"{_collectorBaseUrl}/api/v1/logs?query={Uri.EscapeDataString(query)}&limit={maxResults}";
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Log search failed with status {StatusCode}", response.StatusCode);
                return [];
            }

            var logs = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
            return ParseLogResults(logs);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Log search failed for query: {Query}", query);
            return [];
        }
    }

    /// <summary>
    ///     Searches both spans and logs, returning unified results.
    ///     Results are formatted as context text suitable for agent consumption.
    /// </summary>
    /// <param name="query">Search query.</param>
    /// <param name="maxResults">Maximum total results (split between spans and logs).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Combined search results as formatted context string.</returns>
    public async Task<string> SearchAsContextAsync(
        string query,
        int maxResults = 20,
        CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var halfLimit = Math.Max(1, maxResults / 2);

        var spanTask = SearchSpansAsync(query, halfLimit, ct);
        var logTask = SearchLogsAsync(query, halfLimit, ct);

        await Task.WhenAll(spanTask, logTask).ConfigureAwait(false);

        var spans = await spanTask.ConfigureAwait(false);
        var logs = await logTask.ConfigureAwait(false);

        var sb = new System.Text.StringBuilder();

        if (spans.Count > 0)
        {
            sb.AppendLine("## Matching Spans");
            foreach (var result in spans)
            {
                sb.AppendLine($"- [{result.Timestamp:u}] {result.Name}: {result.Summary}");
            }

            sb.AppendLine();
        }

        if (logs.Count > 0)
        {
            sb.AppendLine("## Matching Logs");
            foreach (var result in logs)
            {
                sb.AppendLine($"- [{result.Timestamp:u}] {result.Name}: {result.Summary}");
            }
        }

        return sb.Length > 0 ? sb.ToString() : "No telemetry data matched the query.";
    }

    /// <summary>
    ///     Creates a <see cref="TextSearchProviderOptions"/> configured for qyl telemetry search.
    /// </summary>
    /// <param name="maxResults">Maximum search results per query.</param>
    /// <returns>Options for use with <see cref="TextSearchProvider"/>.</returns>
    public TextSearchProviderOptions CreateSearchOptions(int maxResults = 20)
    {
        return new TextSearchProviderOptions
        {
            SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
            RecentMessageMemoryLimit = maxResults
        };
    }

    private static List<TelemetrySearchResult> ParseSpanResults(JsonElement element)
    {
        var results = new List<TelemetrySearchResult>();

        if (element.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in element.EnumerateArray())
        {
            results.Add(new TelemetrySearchResult
            {
                Kind = TelemetryResultKind.Span,
                Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                Summary = item.TryGetProperty("status", out var status)
                    ? status.GetString() ?? ""
                    : "",
                Timestamp = item.TryGetProperty("startTime", out var ts) &&
                            DateTimeOffset.TryParse(ts.GetString(), out var parsed)
                    ? parsed
                    : TimeProvider.System.GetUtcNow(),
                TraceId = item.TryGetProperty("traceId", out var tid) ? tid.GetString() : null,
                RawJson = item.GetRawText()
            });
        }

        return results;
    }

    private static List<TelemetrySearchResult> ParseLogResults(JsonElement element)
    {
        var results = new List<TelemetrySearchResult>();

        if (element.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in element.EnumerateArray())
        {
            results.Add(new TelemetrySearchResult
            {
                Kind = TelemetryResultKind.Log,
                Name = item.TryGetProperty("severityText", out var sev)
                    ? sev.GetString() ?? "INFO"
                    : "INFO",
                Summary = item.TryGetProperty("body", out var body)
                    ? body.GetString() ?? ""
                    : "",
                Timestamp = item.TryGetProperty("timestamp", out var ts) &&
                            DateTimeOffset.TryParse(ts.GetString(), out var parsed)
                    ? parsed
                    : TimeProvider.System.GetUtcNow(),
                TraceId = item.TryGetProperty("traceId", out var tid) ? tid.GetString() : null,
                RawJson = item.GetRawText()
            });
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
///     Kind of telemetry result.
/// </summary>
public enum TelemetryResultKind
{
    /// <summary>Result from a span/trace.</summary>
    Span,

    /// <summary>Result from a log record.</summary>
    Log
}

/// <summary>
///     A single telemetry search result.
/// </summary>
public sealed record TelemetrySearchResult
{
    /// <summary>Kind of telemetry data.</summary>
    public required TelemetryResultKind Kind { get; init; }

    /// <summary>Name or identifier (span name or severity).</summary>
    public required string Name { get; init; }

    /// <summary>Summary text (status or body).</summary>
    public required string Summary { get; init; }

    /// <summary>When this telemetry was recorded.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Associated trace ID for correlation.</summary>
    public string? TraceId { get; init; }

    /// <summary>Raw JSON for detailed inspection.</summary>
    public string? RawJson { get; init; }
}
