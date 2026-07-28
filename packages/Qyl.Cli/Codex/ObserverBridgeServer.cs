using System.Text.Json;

namespace Qyl.Cli.Codex;

internal static class ObserverBridgeServer
{
    private const int MaxMessageCharacters = 1024 * 1024;
    private const string ProtocolVersion = "2026-07-28";
    private const string ProtocolVersionMetaKey = "io.modelcontextprotocol/protocolVersion";
    private const string ClientInfoMetaKey = "io.modelcontextprotocol/clientInfo";
    private const string ClientCapabilitiesMetaKey = "io.modelcontextprotocol/clientCapabilities";
    private const string ServerInfoMetaKey = "io.modelcontextprotocol/serverInfo";
    private const string ToolName = "get_active_workflow_run";

    public static async Task<int> RunAsync(
        ActiveWorkflowRunStore activeRuns,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                return 0;
            if (line.Length > MaxMessageCharacters)
            {
                await WriteErrorAsync(output, null, -32600, "MCP request exceeds the 1 MiB limit.")
                    .ConfigureAwait(false);
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
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
                        await WriteToolResultAsync(output, id, request, activeRuns.Read())
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
                    "Read the active qyl Codex workflow run through get_active_workflow_run.");
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
                writer.WriteString("name", ToolName);
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
                writer.WriteEndArray();
                WritePublicCache(writer);
            }).ConfigureAwait(false);
    }

    private static async Task WriteToolResultAsync(
        TextWriter output,
        JsonElement? id,
        JsonElement request,
        ActiveWorkflowRun? active)
    {
        if (!request.TryGetProperty("params", out var parameters) ||
            !parameters.TryGetProperty("name", out var nameElement) ||
            nameElement.GetString() != ToolName)
        {
            await WriteErrorAsync(output, id, -32602, $"Unknown tool. Expected '{ToolName}'.")
                .ConfigureAwait(false);
            return;
        }

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

    private static Task WriteEmptyResultAsync(TextWriter output, JsonElement? id) =>
        WriteAsync(
            output,
            id,
            static _ => { });

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
