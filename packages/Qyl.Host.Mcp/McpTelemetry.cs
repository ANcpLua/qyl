using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Host.Mcp;

/// <summary>
/// OTLP self-monitoring of the MCP passthrough — the C# port of qyl.mcp's <c>telemetry.ts</c>.
/// One CLIENT span per passthrough call, batched and POSTed as OTLP/JSON to
/// <c>{QYL_OTLP_ENDPOINT}/v1/traces</c> (default <c>http://127.0.0.1:4318</c>). Instrumenting
/// here monitors every managed MCP server without touching any of them.
/// </summary>
/// <remarks>
/// Config: <c>QYL_MCP_TELEMETRY=0</c> disables; <c>QYL_MCP_RECORD_INPUTS=1</c> /
/// <c>QYL_MCP_RECORD_OUTPUTS=1</c> gate argument/result capture (off by default — they may
/// contain user content). Ids are spec-hex per the collector's strict OTLP/JSON contract.
/// Failures never throw into the passthrough: an unreachable collector logs one notice and
/// batches are dropped.
/// </remarks>
public sealed partial class McpTelemetry : IHostedService, IAsyncDisposable
{
    private const int MaxQueue = 512;
    private const int RecordedValueMaxChars = 2000;
    private static readonly TimeSpan s_flushInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_exportTimeout = TimeSpan.FromSeconds(3);

    private readonly HttpClient _http = new();
    private readonly Lock _lock = new();
    private readonly List<PendingSpan> _queue = [];
    private readonly string _endpoint;
    private readonly bool _enabled;
    private readonly string _sessionId = Guid.NewGuid().ToString();
    private readonly bool _recordInputs;
    private readonly bool _recordOutputs;
    private readonly TimeProvider _time;
    private readonly ILogger<McpTelemetry> _logger;
    private ITimer? _timer;
    private bool _unreachableNoticeShown;

    public McpTelemetry(TimeProvider time, ILogger<McpTelemetry> logger)
    {
        _time = time;
        _logger = logger;
        _endpoint = (Environment.GetEnvironmentVariable("QYL_OTLP_ENDPOINT") ?? "http://127.0.0.1:4318")
            .TrimEnd('/');
        _enabled = Environment.GetEnvironmentVariable("QYL_MCP_TELEMETRY") != "0";
        _recordInputs = Environment.GetEnvironmentVariable("QYL_MCP_RECORD_INPUTS") == "1";
        _recordOutputs = Environment.GetEnvironmentVariable("QYL_MCP_RECORD_OUTPUTS") == "1";
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_enabled)
            _timer = _time.CreateTimer(_ => _ = FlushAsync(), null, s_flushInterval, s_flushInterval);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;
        await FlushAsync().ConfigureAwait(false);
    }

    /// <summary>Queues one CLIENT span for a completed passthrough call. Never throws.</summary>
    public void RecordCall(McpCallRecord call)
    {
        if (!_enabled) return;

        var attributes = new List<KeyValuePair<string, string>>
        {
            new("mcp.method.name", call.Method),
            new("mcp.server.name", call.ServerName),
            new("app.transport", call.Transport)
        };

        if (call.ToolName is { } toolName)
        {
            // Both spellings on purpose: mcp.tool.name is the MCP convention, gen_ai.tool.name is
            // what the qyl collector's GenAI pipeline joins on (same dual-key as telemetry.ts).
            attributes.Add(new KeyValuePair<string, string>("mcp.tool.name", toolName));
            attributes.Add(new KeyValuePair<string, string>("gen_ai.tool.name", toolName));
        }

        if (call.ResourceUri is { } resourceUri)
            attributes.Add(new KeyValuePair<string, string>("mcp.resource.uri", resourceUri));

        if (call.Error is not null)
            attributes.Add(new KeyValuePair<string, string>("error.type", "mcp_error"));

        if (_recordInputs && call.Arguments is { } arguments)
        {
            foreach (var (key, value) in arguments)
                attributes.Add(new KeyValuePair<string, string>($"gen_ai.tool.call.arguments.{key}", Truncate(value)));
        }

        if (_recordOutputs && call.ResultJson is { } resultJson)
        {
            attributes.Add(new KeyValuePair<string, string>("gen_ai.tool.call.result", Truncate(resultJson)));
            if (call.ResultContentCount is { } count)
            {
                attributes.Add(new KeyValuePair<string, string>("gen_ai.tool.call.result.count",
                    count.ToString(CultureInfo.InvariantCulture)));
            }
        }

        var target = call.ToolName ?? call.ResourceUri;
        var span = new PendingSpan(
            TraceId: RandomHexId(16),
            SpanId: RandomHexId(8),
            Name: target is null ? call.Method : $"{call.Method} {target}",
            StartUnixNano: ToUnixNanos(call.StartTime),
            EndUnixNano: ToUnixNanos(call.EndTime),
            StatusCode: call.Error is null ? 1 : 2,
            StatusMessage: call.Error,
            Attributes: attributes);

        lock (_lock)
        {
            if (_queue.Count >= MaxQueue) _queue.RemoveAt(0);
            _queue.Add(span);
        }
    }

    private async Task FlushAsync()
    {
        PendingSpan[] spans;
        lock (_lock)
        {
            if (_queue.Count is 0) return;
            spans = [.. _queue];
            _queue.Clear();
        }

        try
        {
            using var timeoutCts = new CancellationTokenSource(s_exportTimeout);
            using var content = new ByteArrayContent(BuildOtlpPayload(spans));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            using var response = await _http.PostAsync($"{_endpoint}/v1/traces", content, timeoutCts.Token)
                .ConfigureAwait(false);
            _ = response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            if (!_unreachableNoticeShown)
            {
                _unreachableNoticeShown = true;
                LogCollectorUnreachable(_endpoint, ex.Message);
            }
        }
    }

    private byte[] BuildOtlpPayload(PendingSpan[] spans)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        using var writer = new Utf8JsonWriter(buffer);

        writer.WriteStartObject();
        writer.WriteStartArray("resourceSpans");
        writer.WriteStartObject();

        writer.WriteStartObject("resource");
        writer.WriteStartArray("attributes");
        WriteStringAttribute(writer, "service.name", "qyl.host.mcp");
        WriteStringAttribute(writer, "session.id", _sessionId);
        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.WriteStartArray("scopeSpans");
        writer.WriteStartObject();
        writer.WriteStartObject("scope");
        writer.WriteString("name", "qyl.host.mcp/passthrough");
        writer.WriteEndObject();

        writer.WriteStartArray("spans");
        foreach (var span in spans)
        {
            writer.WriteStartObject();
            writer.WriteString("traceId", span.TraceId);
            writer.WriteString("spanId", span.SpanId);
            writer.WriteString("name", span.Name);
            writer.WriteNumber("kind", 3); // SPAN_KIND_CLIENT — the runner calls the managed server
            writer.WriteString("startTimeUnixNano", span.StartUnixNano.ToString(CultureInfo.InvariantCulture));
            writer.WriteString("endTimeUnixNano", span.EndUnixNano.ToString(CultureInfo.InvariantCulture));

            writer.WriteStartArray("attributes");
            foreach (var (key, value) in span.Attributes)
                WriteStringAttribute(writer, key, value);
            writer.WriteEndArray();

            writer.WriteStartObject("status");
            writer.WriteNumber("code", span.StatusCode);
            if (span.StatusMessage is { } message) writer.WriteString("message", message);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        return buffer.WrittenSpan.ToArray();
    }

    private static void WriteStringAttribute(Utf8JsonWriter writer, string key, string value)
    {
        writer.WriteStartObject();
        writer.WriteString("key", key);
        writer.WriteStartObject("value");
        writer.WriteString("stringValue", value);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    // Spec-hex, lowercase — the qyl collector rejects anything else since its repair-plan phase 1.
    private static string RandomHexId(int bytes) =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(bytes));

    private static ulong ToUnixNanos(DateTimeOffset timestamp) =>
        (ulong)timestamp.ToUnixTimeMilliseconds() * 1_000_000UL;

    private static string Truncate(string value) =>
        value.Length <= RecordedValueMaxChars ? value : value[..RecordedValueMaxChars] + "…";

    public async ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        await FlushAsync().ConfigureAwait(false);
        _http.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "MCP telemetry collector unreachable at {Endpoint} — dropping batches ({Reason})")]
    private partial void LogCollectorUnreachable(string endpoint, string reason);

    private sealed record PendingSpan(
        string TraceId,
        string SpanId,
        string Name,
        ulong StartUnixNano,
        ulong EndUnixNano,
        int StatusCode,
        string? StatusMessage,
        List<KeyValuePair<string, string>> Attributes);
}
