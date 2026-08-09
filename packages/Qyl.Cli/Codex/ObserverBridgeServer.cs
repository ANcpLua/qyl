using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qyl.Cli.Codex;

internal static class ObserverBridgeServer
{
    private const int MaxMessageCharacters = 256 * 1024;
    private const string ProtocolVersion = "2026-07-28";
    private const string ProtocolVersionMetaKey = "io.modelcontextprotocol/protocolVersion";
    private const string ClientInfoMetaKey = "io.modelcontextprotocol/clientInfo";
    private const string ClientCapabilitiesMetaKey = "io.modelcontextprotocol/clientCapabilities";
    private const string ServerInfoMetaKey = "io.modelcontextprotocol/serverInfo";
    private const string ReadToolName = "get_active_workflow_run";
    private const string DiagnosticToolName = "record_diagnostic_snapshot";

    public static async Task<int> RunAsync(
        ActiveWorkflowRunStore activeRuns,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var diagnosticInbox = new DiagnosticSnapshotInbox(activeRuns.Root);
        var requestReader = new BoundedLineReader(input, MaxMessageCharacters);
        while (!cancellationToken.IsCancellationRequested)
        {
            var requestLine = await requestReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (requestLine.EndOfStream)
                return 0;
            if (requestLine.ExceedsLimit)
            {
                await WriteErrorAsync(
                        output,
                        null,
                        -32600,
                        "MCP request exceeds the 262,144-character transport limit.")
                    .ConfigureAwait(false);
                continue;
            }
            var line = requestLine.Value!;

            try
            {
                using var document = JsonDocument.Parse(
                    line,
                    new JsonDocumentOptions { MaxDepth = 16 });
                var request = document.RootElement;
                if (!request.TryGetProperty("method", out var methodElement) ||
                    methodElement.ValueKind is not JsonValueKind.String)
                {
                    await WriteErrorAsync(output, RequestId(request), -32600, "Invalid MCP request.")
                        .ConfigureAwait(false);
                    continue;
                }

                var method = methodElement.GetString();
                var id = RequestId(request);
                if (method == "initialize")
                {
                    await WriteUnsupportedProtocolVersionAsync(
                        output,
                        id,
                        RequestedLegacyVersion(request)).ConfigureAwait(false);
                    continue;
                }

                if (!TryValidateModernEnvelope(request, out var invalidKey, out var invalidReason))
                {
                    if (id is not null)
                    {
                        if (invalidKey == ProtocolVersionMetaKey)
                        {
                            await WriteUnsupportedProtocolVersionAsync(
                                output,
                                id,
                                RequestedProtocolVersion(request)).ConfigureAwait(false);
                        }
                        else
                        {
                            await WriteInvalidEnvelopeAsync(
                                output,
                                id,
                                invalidKey,
                                invalidReason).ConfigureAwait(false);
                        }
                    }
                    continue;
                }

                switch (method)
                {
                    case "server/discover":
                        await WriteDiscoverAsync(output, id).ConfigureAwait(false);
                        break;
                    case "notifications/cancelled":
                        break;
                    case "ping":
                        await WriteEmptyResultAsync(output, id).ConfigureAwait(false);
                        break;
                    case "tools/list":
                        await WriteToolsAsync(output, id).ConfigureAwait(false);
                        break;
                    case "tools/call":
                        await WriteToolResultAsync(
                                output,
                                id,
                                request,
                                activeRuns,
                                diagnosticInbox,
                                cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    default:
                        if (id is not null)
                        {
                            await WriteErrorAsync(output, id, -32601, $"Unknown MCP method '{method}'.")
                                .ConfigureAwait(false);
                        }
                        break;
                }
            }
            catch (JsonException)
            {
                await WriteErrorAsync(output, null, -32700, "Invalid MCP JSON.")
                    .ConfigureAwait(false);
            }
        }

        return 0;
    }

    private static async Task WriteDiscoverAsync(TextWriter output, JsonElement? id)
    {
        await WriteAsync(
            output,
            id,
            static writer =>
            {
                writer.WritePropertyName("supportedVersions");
                writer.WriteStartArray();
                writer.WriteStringValue(ProtocolVersion);
                writer.WriteEndArray();
                writer.WritePropertyName("capabilities");
                writer.WriteStartObject();
                writer.WritePropertyName("tools");
                writer.WriteStartObject();
                writer.WriteBoolean("listChanged", false);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteString(
                    "instructions",
                    "Read the active qyl Codex workflow run or record a bounded diagnostic snapshot against it.");
                WritePublicCache(writer);
            }).ConfigureAwait(false);
    }

    private static async Task WriteToolsAsync(TextWriter output, JsonElement? id)
    {
        await WriteAsync(
            output,
            id,
            static writer =>
            {
                writer.WritePropertyName("tools");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("name", ReadToolName);
                writer.WriteString(
                    "description",
                    "Returns the workflow run observed by the active qyl codex process, if one exists.");
                writer.WritePropertyName("inputSchema");
                writer.WriteStartObject();
                writer.WriteString("type", "object");
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteBoolean("additionalProperties", false);
                writer.WriteEndObject();
                writer.WritePropertyName("outputSchema");
                writer.WriteStartObject();
                writer.WriteString("$schema", "https://json-schema.org/draft/2020-12/schema");
                writer.WriteString("type", "object");
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                WriteBooleanSchema(writer, "active");
                WriteBooleanSchema(writer, "liveControlsAvailable");
                WriteStringSchema(writer, "runId");
                WriteStringSchema(writer, "threadId");
                writer.WritePropertyName("startedAt");
                writer.WriteStartObject();
                writer.WriteString("type", "string");
                writer.WriteString("format", "date-time");
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WritePropertyName("required");
                writer.WriteStartArray();
                writer.WriteStringValue("active");
                writer.WriteStringValue("liveControlsAvailable");
                writer.WriteEndArray();
                writer.WriteBoolean("additionalProperties", false);
                writer.WriteEndObject();
                writer.WritePropertyName("annotations");
                writer.WriteStartObject();
                writer.WriteBoolean("readOnlyHint", true);
                writer.WriteBoolean("destructiveHint", false);
                writer.WriteBoolean("idempotentHint", true);
                writer.WriteBoolean("openWorldHint", false);
                writer.WriteEndObject();
                writer.WriteEndObject();
                WriteDiagnosticToolSchema(writer);
                writer.WriteEndArray();
                WritePublicCache(writer);
            }).ConfigureAwait(false);
    }

    private static async Task WriteToolResultAsync(
        TextWriter output,
        JsonElement? id,
        JsonElement request,
        ActiveWorkflowRunStore activeRuns,
        DiagnosticSnapshotInbox diagnosticInbox,
        CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind is not JsonValueKind.String)
        {
            await WriteErrorAsync(output, id, -32602, "Tool name is required.")
                .ConfigureAwait(false);
            return;
        }

        var toolName = nameElement.GetString();
        if (toolName == DiagnosticToolName)
        {
            await WriteDiagnosticToolResultAsync(
                    output,
                    id,
                    parameters,
                    activeRuns.Read(),
                    diagnosticInbox,
                    cancellationToken)
                .ConfigureAwait(false);
            return;
        }
        if (toolName != ReadToolName)
        {
            await WriteErrorAsync(output, id, -32602, $"Unknown tool '{toolName}'.")
                .ConfigureAwait(false);
            return;
        }

        var active = activeRuns.Read();

        await WriteAsync(
            output,
            id,
            writer =>
            {
                writer.WritePropertyName("content");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString(
                    "text",
                    active is null
                        ? "No live qyl codex workflow is active."
                        : $"Live qyl workflow run: {active.RunId}");
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WritePropertyName("structuredContent");
                writer.WriteStartObject();
                writer.WriteBoolean("active", active is not null);
                writer.WriteBoolean("liveControlsAvailable", active is not null);
                if (active is not null)
                {
                    writer.WriteString("runId", active.RunId);
                    if (active.ThreadId is not null)
                        writer.WriteString("threadId", active.ThreadId);
                    writer.WriteString("startedAt", active.StartedAt);
                }
                writer.WriteEndObject();
                writer.WriteBoolean("isError", false);
            }).ConfigureAwait(false);
    }

    private static async Task WriteDiagnosticToolResultAsync(
        TextWriter output,
        JsonElement? id,
        JsonElement parameters,
        ActiveWorkflowRun? active,
        DiagnosticSnapshotInbox diagnosticInbox,
        CancellationToken cancellationToken)
    {
        if (!parameters.TryGetProperty("arguments", out var arguments) ||
            arguments.ValueKind is not JsonValueKind.Object)
        {
            await WriteDiagnosticResultAsync(
                    output,
                    id,
                    new DiagnosticSnapshotSubmissionResult(false, "invalid_input", "unknown", null),
                    "arguments")
                .ConfigureAwait(false);
            return;
        }

        var snapshotId = arguments.TryGetProperty("snapshotId", out var snapshotElement) &&
                         snapshotElement.ValueKind is JsonValueKind.String
            ? snapshotElement.GetString() ?? "unknown"
            : "unknown";
        if (active is null)
        {
            await WriteDiagnosticResultAsync(
                    output,
                    id,
                    new DiagnosticSnapshotSubmissionResult(false, "no_active_run", snapshotId, null),
                    null)
                .ConfigureAwait(false);
            return;
        }
        if (active.ThreadId is null)
        {
            await WriteDiagnosticResultAsync(
                    output,
                    id,
                    new DiagnosticSnapshotSubmissionResult(
                        false,
                        "context_unavailable",
                        snapshotId,
                        null),
                    null)
                .ConfigureAwait(false);
            return;
        }

        DiagnosticSnapshotInboxRequest? request;
        DiagnosticSnapshotValidationError validationError;
        try
        {
            if (!DiagnosticSnapshotCapture.TryCreate(
                    active,
                    arguments,
                    diagnosticInbox.Protector,
                    TimeProvider.System.GetUtcNow(),
                    out request,
                    out validationError))
            {
                await WriteDiagnosticResultAsync(
                        output,
                        id,
                        new DiagnosticSnapshotSubmissionResult(
                            false,
                            validationError.Code,
                            snapshotId,
                            null),
                        validationError.Field)
                    .ConfigureAwait(false);
                return;
            }
        }
        catch (InvalidDataException)
        {
            await WriteDiagnosticResultAsync(
                    output,
                    id,
                    new DiagnosticSnapshotSubmissionResult(
                        false,
                        "invalid_json_value",
                        snapshotId,
                        null),
                    "variables")
                .ConfigureAwait(false);
            return;
        }

        DiagnosticSnapshotSubmissionResult result;
        try
        {
            result = await diagnosticInbox.SubmitAsync(
                    request!,
                    TimeSpan.FromSeconds(3),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or CryptographicException)
        {
            result = new DiagnosticSnapshotSubmissionResult(
                false,
                "inbox_failure",
                request!.SnapshotId,
                null);
        }
        await WriteDiagnosticResultAsync(output, id, result, null).ConfigureAwait(false);
    }

    private static Task WriteDiagnosticResultAsync(
        TextWriter output,
        JsonElement? id,
        DiagnosticSnapshotSubmissionResult result,
        string? field) =>
        WriteAsync(
            output,
            id,
            writer =>
            {
                writer.WritePropertyName("content");
                writer.WriteStartArray();
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", result.Code);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WritePropertyName("structuredContent");
                writer.WriteStartObject();
                writer.WriteBoolean("recorded", result.Recorded);
                writer.WriteString("code", result.Code);
                writer.WriteString("snapshotId", result.SnapshotId);
                if (result.EventId is not null)
                    writer.WriteString("eventId", result.EventId);
                if (field is not null)
                    writer.WriteString("field", field);
                writer.WriteEndObject();
                writer.WriteBoolean("isError", !result.Recorded);
            });

    private static Task WriteEmptyResultAsync(TextWriter output, JsonElement? id) =>
        WriteAsync(
            output,
            id,
            static _ => { });

    private static void WriteDiagnosticToolSchema(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("name", DiagnosticToolName);
        writer.WriteString(
            "description",
            "Record one immutable, bounded diagnostic state frame against the active qyl Codex run. " +
            "Reuse the same snapshotId and payload when retrying; changing a payload under an existing " +
            "snapshotId is a conflict. Variable names remain data: public/internal values enter protected " +
            "content, sensitive values are redacted, and secret values are omitted. Checks reference " +
            "variable names and use closed operators; expression strings are not accepted.");
        writer.WritePropertyName("inputSchema");
        writer.WriteStartObject();
        writer.WriteString("$schema", "https://json-schema.org/draft/2020-12/schema");
        writer.WriteString("type", "object");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        WriteMachineIdentifierSchema(writer, "snapshotId");
        WriteMachineIdentifierSchema(writer, "probeId");
        WriteEnumSchema(writer, "phase", ["input", "output", "error", "checkpoint"]);
        writer.WritePropertyName("variables");
        writer.WriteStartObject();
        writer.WriteString("type", "array");
        writer.WriteString(
            "description",
            "Dynamically named typed state. Classification is applied before the frame leaves the bridge process.");
        writer.WriteNumber("maxItems", DiagnosticSnapshotCapture.MaxVariables);
        writer.WritePropertyName("items");
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        WriteMachineIdentifierSchema(writer, "name");
        WriteEnumSchema(writer, "classification", ["public", "internal", "sensitive", "secret"]);
        writer.WritePropertyName("value");
        writer.WriteStartObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        WriteRequired(writer, ["name", "classification", "value"]);
        writer.WriteBoolean("additionalProperties", false);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WritePropertyName("checks");
        writer.WriteStartObject();
        writer.WriteString("type", "array");
        writer.WriteString(
            "description",
            "Structural checks whose actual/expected operands are variable-name references. Missing or incompatible operands are unknown, except exists on absent/null state is fail.");
        writer.WriteNumber("maxItems", DiagnosticSnapshotCapture.MaxChecks);
        writer.WritePropertyName("default");
        writer.WriteStartArray();
        writer.WriteEndArray();
        writer.WritePropertyName("items");
        writer.WriteStartObject();
        writer.WriteString("type", "object");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        WriteMachineIdentifierSchema(writer, "checkId");
        WriteEnumSchema(
            writer,
            "operator",
            ["equal", "not_equal", "exists", "type_is", "contains", "less_than", "greater_than"]);
        WriteMachineIdentifierSchema(writer, "actual");
        WriteMachineIdentifierSchema(writer, "expected");
        WriteEnumSchema(
            writer,
            "expectedType",
            ["null", "boolean", "integer", "number", "string", "json"]);
        writer.WriteEndObject();
        WriteRequired(writer, ["checkId", "operator", "actual"]);
        writer.WriteBoolean("additionalProperties", false);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        WriteRequired(writer, ["snapshotId", "probeId", "phase", "variables"]);
        writer.WriteBoolean("additionalProperties", false);
        writer.WriteEndObject();

        writer.WritePropertyName("outputSchema");
        writer.WriteStartObject();
        writer.WriteString("$schema", "https://json-schema.org/draft/2020-12/schema");
        writer.WriteString("type", "object");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        WriteBooleanSchema(writer, "recorded");
        WriteStringSchema(writer, "code");
        WriteStringSchema(writer, "snapshotId");
        WriteStringSchema(writer, "eventId");
        WriteStringSchema(writer, "field");
        writer.WriteEndObject();
        WriteRequired(writer, ["recorded", "code", "snapshotId"]);
        writer.WriteBoolean("additionalProperties", false);
        writer.WriteEndObject();
        writer.WritePropertyName("annotations");
        writer.WriteStartObject();
        writer.WriteBoolean("readOnlyHint", false);
        writer.WriteBoolean("destructiveHint", false);
        writer.WriteBoolean("idempotentHint", true);
        writer.WriteBoolean("openWorldHint", false);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteMachineIdentifierSchema(Utf8JsonWriter writer, string name)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("type", "string");
        writer.WriteNumber("minLength", 1);
        writer.WriteNumber("maxLength", DiagnosticSnapshotCapture.MaxIdentifierLength);
        writer.WriteString("pattern", "^[A-Za-z0-9_][A-Za-z0-9._:/\\[\\]-]*$");
        writer.WriteEndObject();
    }

    private static void WriteEnumSchema(
        Utf8JsonWriter writer,
        string name,
        IReadOnlyList<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("type", "string");
        writer.WritePropertyName("enum");
        writer.WriteStartArray();
        foreach (var value in values)
            writer.WriteStringValue(value);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRequired(Utf8JsonWriter writer, IReadOnlyList<string> names)
    {
        writer.WritePropertyName("required");
        writer.WriteStartArray();
        foreach (var name in names)
            writer.WriteStringValue(name);
        writer.WriteEndArray();
    }

    private static bool TryValidateModernEnvelope(
        JsonElement request,
        out string invalidKey,
        out string invalidReason)
    {
        invalidKey = ProtocolVersionMetaKey;
        invalidReason = "missing";
        if (!request.TryGetProperty("params", out var parameters) ||
            parameters.ValueKind is not JsonValueKind.Object ||
            !parameters.TryGetProperty("_meta", out var metadata) ||
            metadata.ValueKind is not JsonValueKind.Object)
        {
            return false;
        }

        if (!metadata.TryGetProperty(ProtocolVersionMetaKey, out var version) ||
            version.ValueKind is not JsonValueKind.String ||
            version.GetString() != ProtocolVersion)
        {
            invalidReason = version.ValueKind is JsonValueKind.String ? "unsupported" : "missing";
            return false;
        }

        invalidKey = ClientCapabilitiesMetaKey;
        if (!metadata.TryGetProperty(ClientCapabilitiesMetaKey, out var capabilities))
        {
            invalidReason = "missing";
            return false;
        }
        if (capabilities.ValueKind is not JsonValueKind.Object)
        {
            invalidReason = "must be an object";
            return false;
        }

        if (metadata.TryGetProperty(ClientInfoMetaKey, out var clientInfo) &&
            !IsImplementation(clientInfo))
        {
            invalidKey = ClientInfoMetaKey;
            invalidReason = "must contain string name and version";
            return false;
        }

        invalidKey = "";
        invalidReason = "";
        return true;
    }

    private static bool IsImplementation(JsonElement value) =>
        value.ValueKind is JsonValueKind.Object &&
        value.TryGetProperty("name", out var name) &&
        name.ValueKind is JsonValueKind.String &&
        value.TryGetProperty("version", out var version) &&
        version.ValueKind is JsonValueKind.String;

    private static string? RequestedProtocolVersion(JsonElement request) =>
        request.TryGetProperty("params", out var parameters) &&
        parameters.ValueKind is JsonValueKind.Object &&
        parameters.TryGetProperty("_meta", out var metadata) &&
        metadata.ValueKind is JsonValueKind.Object &&
        metadata.TryGetProperty(ProtocolVersionMetaKey, out var version) &&
        version.ValueKind is JsonValueKind.String
            ? version.GetString()
            : null;

    private static string? RequestedLegacyVersion(JsonElement request) =>
        request.TryGetProperty("params", out var parameters) &&
        parameters.ValueKind is JsonValueKind.Object &&
        parameters.TryGetProperty("protocolVersion", out var version) &&
        version.ValueKind is JsonValueKind.String
            ? version.GetString()
            : null;

    private static Task WriteUnsupportedProtocolVersionAsync(
        TextWriter output,
        JsonElement? id,
        string? requested) =>
        WriteErrorAsync(
            output,
            id,
            -32022,
            requested is null
                ? "Unsupported protocol version: the request did not name a protocol version"
                : $"Unsupported protocol version: {requested}",
            writer =>
            {
                writer.WritePropertyName("supported");
                writer.WriteStartArray();
                writer.WriteStringValue(ProtocolVersion);
                writer.WriteEndArray();
                if (requested is not null)
                    writer.WriteString("requested", requested);
            });

    private static Task WriteInvalidEnvelopeAsync(
        TextWriter output,
        JsonElement? id,
        string key,
        string reason) =>
        WriteErrorAsync(
            output,
            id,
            -32602,
            $"Invalid _meta envelope for protocol revision {ProtocolVersion}: {key}: {reason}",
            writer =>
            {
                writer.WritePropertyName("envelope");
                writer.WriteStartObject();
                writer.WriteString("key", key);
                writer.WriteString("problem", reason);
                writer.WriteEndObject();
            });

    private static async Task WriteErrorAsync(
        TextWriter output,
        JsonElement? id,
        int code,
        string message,
        Action<Utf8JsonWriter>? writeData = null)
    {
        await WriteEnvelopeAsync(
            output,
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("jsonrpc", "2.0");
                WriteId(writer, id);
                writer.WritePropertyName("error");
                writer.WriteStartObject();
                writer.WriteNumber("code", code);
                writer.WriteString("message", message);
                if (writeData is not null)
                {
                    writer.WritePropertyName("data");
                    writer.WriteStartObject();
                    writeData(writer);
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }).ConfigureAwait(false);
    }

    private static async Task WriteAsync(
        TextWriter output,
        JsonElement? id,
        Action<Utf8JsonWriter> writeResult)
    {
        await WriteEnvelopeAsync(
            output,
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("jsonrpc", "2.0");
                WriteId(writer, id);
                writer.WritePropertyName("result");
                writer.WriteStartObject();
                writeResult(writer);
                writer.WriteString("resultType", "complete");
                writer.WritePropertyName("_meta");
                writer.WriteStartObject();
                writer.WritePropertyName(ServerInfoMetaKey);
                writer.WriteStartObject();
                writer.WriteString("name", "qyl-observer-bridge");
                writer.WriteString("version", BuildVersion.ProductVersion);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }).ConfigureAwait(false);
    }

    private static void WritePublicCache(Utf8JsonWriter writer)
    {
        writer.WriteNumber("ttlMs", 300_000);
        writer.WriteString("cacheScope", "public");
    }

    private static void WriteBooleanSchema(Utf8JsonWriter writer, string name)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("type", "boolean");
        writer.WriteEndObject();
    }

    private readonly record struct BoundedLine(string? Value, bool ExceedsLimit)
    {
        public bool EndOfStream => Value is null && !ExceedsLimit;
    }

    private sealed class BoundedLineReader(TextReader reader, int maximumCharacters)
    {
        private readonly char[] _buffer = new char[4 * 1024];
        private int _count;
        private int _offset;

        public async ValueTask<BoundedLine> ReadAsync(CancellationToken cancellationToken)
        {
            var value = new StringBuilder(Math.Min(maximumCharacters, _buffer.Length));
            var sawCharacters = false;
            var exceedsLimit = false;
            while (true)
            {
                if (_offset == _count)
                {
                    _count = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken)
                        .ConfigureAwait(false);
                    _offset = 0;
                    if (_count == 0)
                    {
                        return !sawCharacters
                            ? new BoundedLine(null, false)
                            : Complete(value, exceedsLimit);
                    }
                }

                var remaining = _buffer.AsSpan(_offset, _count - _offset);
                var newline = remaining.IndexOf('\n');
                var segmentLength = newline < 0 ? remaining.Length : newline;
                sawCharacters |= segmentLength > 0;
                if (!exceedsLimit)
                {
                    if (segmentLength > maximumCharacters - value.Length)
                    {
                        exceedsLimit = true;
                        value.Clear();
                    }
                    else
                    {
                        value.Append(remaining[..segmentLength]);
                    }
                }
                _offset += segmentLength;

                if (newline < 0)
                    continue;
                _offset++;
                return Complete(value, exceedsLimit);
            }
        }

        private static BoundedLine Complete(StringBuilder value, bool exceedsLimit)
        {
            if (exceedsLimit)
                return new BoundedLine(null, true);
            if (value.Length > 0 && value[^1] == '\r')
                value.Length--;
            return new BoundedLine(value.ToString(), false);
        }
    }

    private static void WriteStringSchema(Utf8JsonWriter writer, string name)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        writer.WriteString("type", "string");
        writer.WriteEndObject();
    }

    private static async Task WriteEnvelopeAsync(
        TextWriter output,
        Action<Utf8JsonWriter> write)
    {
        var stream = new MemoryStream();
        await using var streamScope = stream.ConfigureAwait(false);
        var writer = new Utf8JsonWriter(stream);
        await using var writerScope = writer.ConfigureAwait(false);
        write(writer);
        writer.Flush();
        await output.WriteLineAsync(
            System.Text.Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length)))
            .ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }

    private static JsonElement? RequestId(JsonElement request) =>
        request.TryGetProperty("id", out var id) ? id.Clone() : null;

    private static void WriteId(Utf8JsonWriter writer, JsonElement? id)
    {
        writer.WritePropertyName("id");
        if (id is null)
            writer.WriteNullValue();
        else
            id.Value.WriteTo(writer);
    }
}
