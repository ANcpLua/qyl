using System.Text.Json;

namespace Qyl.Cli.Codex;

internal static class ObserverBridgeServer
{
    private const int MaxMessageCharacters = 1024 * 1024;
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
                switch (method)
                {
                    case "initialize":
                        await WriteInitializeAsync(output, id).ConfigureAwait(false);
                        break;
                    case "notifications/initialized":
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

    private static async Task WriteInitializeAsync(TextWriter output, JsonElement? id)
    {
        await WriteAsync(
            output,
            id,
            static writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("protocolVersion", "2025-06-18");
                writer.WritePropertyName("capabilities");
                writer.WriteStartObject();
                writer.WritePropertyName("tools");
                writer.WriteStartObject();
                writer.WriteBoolean("listChanged", false);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WritePropertyName("serverInfo");
                writer.WriteStartObject();
                writer.WriteString("name", "qyl-observer-bridge");
                writer.WriteString("version", BuildVersion.ProductVersion);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }).ConfigureAwait(false);
    }

    private static async Task WriteToolsAsync(TextWriter output, JsonElement? id)
    {
        await WriteAsync(
            output,
            id,
            static writer =>
            {
                writer.WriteStartObject();
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
                writer.WritePropertyName("annotations");
                writer.WriteStartObject();
                writer.WriteBoolean("readOnlyHint", true);
                writer.WriteBoolean("destructiveHint", false);
                writer.WriteBoolean("idempotentHint", true);
                writer.WriteBoolean("openWorldHint", false);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
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
                writer.WriteStartObject();
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
                writer.WriteEndObject();
            }).ConfigureAwait(false);
    }

    private static Task WriteEmptyResultAsync(TextWriter output, JsonElement? id) =>
        WriteAsync(
            output,
            id,
            static writer =>
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            });

    private static async Task WriteErrorAsync(
        TextWriter output,
        JsonElement? id,
        int code,
        string message)
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
                writeResult(writer);
                writer.WriteEndObject();
            }).ConfigureAwait(false);
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
