
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using JsonrpcAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Jsonrpc.JsonrpcAttributes;
using RpcAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Rpc.RpcAttributes;

namespace Qyl.Instrumentation.Instrumentation.Mcp;

public sealed class QylMcpInstrumentationOptions
{
    public bool RecordInputs { get; set; }

    public bool RecordOutputs { get; set; }

    public int MaxAttributeValueLength { get; set; } = 4_000;

    public string? Transport { get; set; }
}

public static class QylMcpServerInstrumentation
{
    public static IMcpServerBuilder UseQylMcpInstrumentation(
        this IMcpServerBuilder builder,
        ActivitySource activitySource,
        Action<QylMcpInstrumentationOptions>? configure = null)
    {
        Guard.NotNull(builder);
        Guard.NotNull(activitySource);

        var options = new QylMcpInstrumentationOptions();
        configure?.Invoke(options);

        builder.WithMessageFilters(filters =>
        {
            filters.AddIncomingFilter(next => async (context, ct) =>
            {
                var method = context.JsonRpcMessage switch
                {
                    JsonRpcRequest request => request.Method,
                    JsonRpcNotification notification => notification.Method,
                    _ => null
                };

                using var activity = activitySource.StartActivity(
                    method is not null ? $"mcp.receive {method}" : "mcp.receive",
                    ActivityKind.Server);

                if (activity is not null)
                {
                    if (method is not null)
                    {
                        activity.SetTag(RpcAttributes.SystemName, "jsonrpc");
                        activity.SetTag(RpcAttributes.Method, method);
                        activity.SetTag(JsonrpcAttributes.ProtocolVersion, "2.0");
                    }

                    if (context.JsonRpcMessage is JsonRpcRequest req)
                        activity.SetTag(JsonrpcAttributes.RequestId, req.Id.ToString());

                    TagServerContext(activity, context.Server, options.Transport);
                }

                try
                {
                    await next(context, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (RecordAndPropagate(activity, ex))
                {
                    throw;
                }
            });

            filters.AddOutgoingFilter(next => async (context, ct) =>
            {
                using var activity = activitySource.StartActivity(
                    "mcp.send",
                    ActivityKind.Client);

                if (activity is not null)
                {
                    activity.SetTag(RpcAttributes.SystemName, "jsonrpc");
                    switch (context.JsonRpcMessage)
                    {
                        case JsonRpcResponse response:
                            activity.SetTag(JsonrpcAttributes.RequestId, response.Id.ToString());
                            break;
                        case JsonRpcRequest request:
                            activity.SetTag(RpcAttributes.Method, request.Method);
                            activity.SetTag(JsonrpcAttributes.RequestId, request.Id.ToString());
                            break;
                        case JsonRpcNotification notification:
                            activity.SetTag(RpcAttributes.Method, notification.Method);
                            break;
                    }
                    TagServerContext(activity, context.Server, options.Transport);
                }

                await next(context, ct).ConfigureAwait(false);
            });
        });

        builder.WithRequestFilters(filters =>
        {
            filters.AddCallToolFilter(next => async (request, ct) =>
            {
                var toolName = request.Params?.Name ?? "<unknown>";

                using var activity = activitySource.StartActivity(
                    $"{GenAiAttributes.OperationNameValues.ExecuteTool} {toolName}");

                if (activity is not null)
                {
                    activity.SetTag(GenAiAttributes.OperationName, GenAiAttributes.OperationNameValues.ExecuteTool);
                    activity.SetTag(GenAiAttributes.ToolName, toolName);
                    activity.SetTag(GenAiAttributes.ToolType, "extension");
                    activity.SetTag(McpAttr.McpToolName, toolName);
                    activity.SetTag(RpcAttributes.Method, "tools/call");
                    TagServerContext(activity, request.Server, options.Transport);

                    if (options.RecordInputs && request.Params?.Arguments is { } args)
                        activity.SetTag(McpAttr.McpToolArguments, Truncate(SerializeArguments(args), options.MaxAttributeValueLength));
                }

                try
                {
                    var result = await next(request, ct).ConfigureAwait(false);

                    if (activity is not null)
                    {
                        if (result.IsError is true)
                            activity.SetStatus(ActivityStatusCode.Error, "Tool returned IsError");

                        if (options.RecordOutputs)
                        {
                            var totalChars = result.Content
                                .OfType<TextContentBlock>()
                                .Sum(static c => c.Text.Length);
                            activity.SetTag(McpAttr.McpToolResultCharCount, totalChars);

                            var firstText = result.Content
                                .OfType<TextContentBlock>()
                                .FirstOrDefault()?.Text;
                            if (firstText is not null)
                                activity.SetTag(McpAttr.McpToolResult, Truncate(firstText, options.MaxAttributeValueLength));
                        }
                    }

                    return result;
                }
                catch (Exception ex) when (RecordAndPropagate(activity, ex))
                {
                    throw;
                }
            });

            filters.AddReadResourceFilter(next => async (request, ct) =>
            {
                var uri = request.Params?.Uri ?? "<unknown>";

                using var activity = activitySource.StartActivity($"mcp.resource.read {uri}");
                activity?.SetTag(McpAttr.McpResourceUri, uri);
                activity?.SetTag(RpcAttributes.Method, "resources/read");
                TagServerContext(activity, request.Server, options.Transport);

                try
                {
                    var result = await next(request, ct).ConfigureAwait(false);

                    if (activity is not null && options.RecordOutputs)
                    {
                        var totalChars = result.Contents.Sum(static c => c switch
                        {
                            TextResourceContents text => text.Text.Length,
                            BlobResourceContents blob => blob.Blob.Length,
                            _ => 0
                        });
                        activity.SetTag(McpAttr.McpResourceContentCharCount, totalChars);
                    }

                    return result;
                }
                catch (Exception ex) when (RecordAndPropagate(activity, ex))
                {
                    throw;
                }
            });

            filters.AddGetPromptFilter(next => async (request, ct) =>
            {
                var promptName = request.Params?.Name ?? "<unknown>";

                using var activity = activitySource.StartActivity($"mcp.prompt.get {promptName}");
                activity?.SetTag(McpAttr.McpPromptName, promptName);
                activity?.SetTag(RpcAttributes.Method, "prompts/get");
                TagServerContext(activity, request.Server, options.Transport);

                if (activity is not null && options.RecordInputs && request.Params?.Arguments is { } args)
                    activity.SetTag(McpAttr.McpPromptArguments, Truncate(SerializeArguments(args), options.MaxAttributeValueLength));

                try
                {
                    var result = await next(request, ct).ConfigureAwait(false);

                    if (activity is not null && options.RecordOutputs)
                        activity.SetTag(McpAttr.McpPromptMessageCount, result.Messages.Count);

                    return result;
                }
                catch (Exception ex) when (RecordAndPropagate(activity, ex))
                {
                    throw;
                }
            });
        });

        return builder;
    }

    private static void TagServerContext(Activity? activity, McpServer server, string? transport)
    {
        if (activity is null) return;

        if (server.ClientInfo is { } info)
        {
            activity.SetTag(McpAttr.McpClientName, info.Name);
            activity.SetTag(McpAttr.McpClientVersion, info.Version);
        }

        if (server.SessionId is { Length: > 0 } sessionId)
            activity.SetTag(McpAttr.McpSessionId, sessionId);

        if (transport is { Length: > 0 })
            activity.SetTag(McpAttr.McpTransport, transport);
    }

    private static bool RecordAndPropagate(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
        return false;
    }

    private static string SerializeArguments(IDictionary<string, JsonElement> args)
    {
        var sb = new StringBuilder("{");
        var first = true;
        foreach (var (key, value) in args)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append('"').Append(key).Append("\":").Append(value.GetRawText());
        }
        sb.Append('}');
        return sb.ToString();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}

internal static class McpAttr
{
    public const string McpClientName = "mcp.client.name";
    public const string McpClientVersion = "mcp.client.version";
    public const string McpSessionId = "mcp.session.id";
    public const string McpTransport = "mcp.transport";

    public const string McpToolName = "mcp.tool.name";
    public const string McpToolArguments = "mcp.tool.call.arguments";
    public const string McpToolResult = "mcp.tool.call.result";
    public const string McpToolResultCharCount = "mcp.tool.call.result_size_chars";

    public const string McpResourceUri = "mcp.resource.uri";
    public const string McpResourceContentCharCount = "mcp.resource.content_size_chars";

    public const string McpPromptName = "mcp.prompt.name";
    public const string McpPromptArguments = "mcp.prompt.get.arguments";
    public const string McpPromptMessageCount = "mcp.prompt.get.message_count";
}
