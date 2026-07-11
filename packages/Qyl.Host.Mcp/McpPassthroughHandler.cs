using System.Net;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Qyl.Host;

namespace Qyl.Host.Mcp;

/// <summary>
/// The <c>/runner/mcp</c> passthrough — the C# port of qyl.mcp's TS runner-api MCP routes:
/// <c>GET /runner/mcp/{name}/tools</c>, <c>POST /runner/mcp/{name}/tools/call</c>,
/// <c>POST /runner/mcp/{name}/resources/read</c>. Unknown name → 404, known but not Ready →
/// 409, server-side call failure → 502. Every completed call is recorded as one CLIENT span
/// by <see cref="McpTelemetry"/>. Request/response bodies are the MCP protocol shapes,
/// (de)serialized with the SDK's own source-generated options.
/// </summary>
public sealed class McpPassthroughHandler(
    McpClientRegistry clients,
    IReadOnlyList<QylResource> resources,
    McpTelemetry telemetry,
    TimeProvider time) : IQylRunnerRequestHandler
{
    private const string Prefix = "/runner/mcp/";

    public async Task<bool> TryHandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = (context.Request.Url?.AbsolutePath ?? string.Empty).TrimEnd('/');
        if (!path.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var segments = path[Prefix.Length..].Split('/');
        var (name, route, method) = segments switch
        {
            [var n, "tools"] => (n, McpRoute.ListTools, "GET"),
            [var n, "tools", "call"] => (n, McpRoute.CallTool, "POST"),
            [var n, "resources", "read"] => (n, McpRoute.ReadResource, "POST"),
            _ => (string.Empty, McpRoute.Unknown, "")
        };

        if (route is McpRoute.Unknown)
        {
            Respond(context, HttpStatusCode.NotFound, """{"error":"unknown mcp route"}""");
            return true;
        }

        if (!string.Equals(context.Request.HttpMethod, method, StringComparison.OrdinalIgnoreCase))
        {
            Respond(context, HttpStatusCode.MethodNotAllowed, """{"error":"method not allowed"}""");
            return true;
        }

        var resource = resources.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
        if (resource is null)
        {
            Respond(context, HttpStatusCode.NotFound, $$"""{"error":"unknown resource '{{name}}'"}""");
            return true;
        }

        if (!clients.TryGet(name, out var client))
        {
            Respond(context, (HttpStatusCode)409, $$"""{"error":"resource '{{name}}' is not ready"}""");
            return true;
        }

        var started = time.GetUtcNow();
        string? toolName = null;
        string? resourceUri = null;
        IReadOnlyDictionary<string, string>? recordedArguments = null;

        try
        {
            string resultJson;
            int? contentCount = null;

            switch (route)
            {
                case McpRoute.ListTools:
                {
                    var result = await client.ListToolsAsync(new ListToolsRequestParams(), cancellationToken)
                        .ConfigureAwait(false);
                    resultJson = JsonSerializer.Serialize(result,
                        McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ListToolsResult)));
                    break;
                }
                case McpRoute.CallTool:
                {
                    var requestParams = await ReadBodyAsync<CallToolRequestParams>(context, cancellationToken)
                        .ConfigureAwait(false);
                    toolName = requestParams.Name;
                    recordedArguments = requestParams.Arguments?.ToDictionary(
                        static kv => kv.Key,
                        static kv => kv.Value.GetRawText(),
                        StringComparer.Ordinal);
                    var result = await client.CallToolAsync(requestParams, cancellationToken).ConfigureAwait(false);
                    contentCount = result.Content.Count;
                    resultJson = JsonSerializer.Serialize(result,
                        McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(CallToolResult)));
                    break;
                }
                default:
                {
                    var requestParams = await ReadBodyAsync<ReadResourceRequestParams>(context, cancellationToken)
                        .ConfigureAwait(false);
                    resourceUri = requestParams.Uri;
                    var result = await client.ReadResourceAsync(requestParams, cancellationToken).ConfigureAwait(false);
                    resultJson = JsonSerializer.Serialize(result,
                        McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ReadResourceResult)));
                    break;
                }
            }

            telemetry.RecordCall(new McpCallRecord
            {
                Method = RouteMethodName(route),
                ServerName = name,
                Transport = resource.Kind,
                ToolName = toolName,
                ResourceUri = resourceUri,
                Arguments = recordedArguments,
                ResultJson = resultJson,
                ResultContentCount = contentCount,
                StartTime = started,
                EndTime = time.GetUtcNow()
            });

            Respond(context, HttpStatusCode.OK, resultJson);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is McpException or JsonException or IOException or InvalidOperationException
                                       or HttpListenerException)
        {
            telemetry.RecordCall(new McpCallRecord
            {
                Method = RouteMethodName(route),
                ServerName = name,
                Transport = resource.Kind,
                ToolName = toolName,
                ResourceUri = resourceUri,
                Arguments = recordedArguments,
                Error = ex.Message,
                StartTime = started,
                EndTime = time.GetUtcNow()
            });

            Respond(context, HttpStatusCode.BadGateway, """{"error":"mcp call failed"}""");
        }

        return true;
    }

    private static string RouteMethodName(McpRoute route) => route switch
    {
        McpRoute.ListTools => "tools/list",
        McpRoute.CallTool => "tools/call",
        _ => "resources/read"
    };

    private static async Task<T> ReadBodyAsync<T>(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var result = await JsonSerializer.DeserializeAsync(context.Request.InputStream,
                (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T)),
                cancellationToken)
            .ConfigureAwait(false);
        return result ?? throw new JsonException("Request body deserialized to null.");
    }

    private static void Respond(HttpListenerContext context, HttpStatusCode status, string json)
    {
        var payload = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = payload.Length;
        context.Response.OutputStream.Write(payload);
        context.Response.Close();
    }

    private enum McpRoute
    {
        Unknown,
        ListTools,
        CallTool,
        ReadResource
    }
}
