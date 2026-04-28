// =============================================================================
// qyl.instrumentation - MCP Server Instrumentation
// One-line facade that wraps an IMcpServerBuilder with the qyl telemetry stack:
// JSON-RPC envelope spans, gen_ai.execute_tool spans for tools/call,
// mcp.resource.read / mcp.prompt.get spans for the other two MCP primitives,
// silent-error capture (IsError on CallToolResult), thrown-exception recording,
// and PII-gated input/output capture. Mirrors the surface of
// Sentry.wrapMcpServerWithSentry while staying inside qyl's OTel SemConv stack.
// =============================================================================

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;

namespace Qyl.Instrumentation.Instrumentation.Mcp;

/// <summary>
///     PII-gated capture controls for tool/resource/prompt arguments and results.
///     Matches Sentry's <c>recordInputs</c> / <c>recordOutputs</c> + the OTel
///     <c>OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT</c> opt-in shape.
/// </summary>
/// <param name="RecordInputs">
///     When <c>true</c>, capture <c>tools/call</c> arguments and <c>prompts/get</c>
///     parameters on the span. Off by default — arguments may carry user PII.
/// </param>
/// <param name="RecordOutputs">
///     When <c>true</c>, capture <c>tools/call</c> result text, <c>resources/read</c>
///     content size, and <c>prompts/get</c> message count on the span. Off by default.
/// </param>
/// <param name="MaxAttributeValueLength">
///     Per-attribute character cap when <see cref="RecordInputs" /> or
///     <see cref="RecordOutputs" /> is on. Long arguments and results are truncated
///     to this length with an ellipsis suffix to stay below collector limits.
/// </param>
public sealed record QylMcpInstrumentationOptions(
    bool RecordInputs = false,
    bool RecordOutputs = false,
    int MaxAttributeValueLength = 4_000);

/// <summary>
///     Wires qyl's MCP-server telemetry stack onto an <see cref="IMcpServerBuilder" />.
///     One call replaces ~80 lines of inline filter wiring and brings parity with
///     Sentry's <c>wrapMcpServerWithSentry</c>: every JSON-RPC envelope, tool call,
///     resource read, and prompt retrieval becomes a span; thrown exceptions and
///     silent <c>IsError</c> tool results both surface on the span as errors.
/// </summary>
public static class QylMcpServerInstrumentation
{
    /// <summary>
    ///     Wraps an <see cref="IMcpServerBuilder" /> with qyl's MCP-server telemetry.
    /// </summary>
    /// <param name="builder">The MCP server builder to wrap.</param>
    /// <param name="activitySource">
    ///     ActivitySource the wrapped filters emit spans on. Callers typically pass
    ///     a service-scoped source (for example <c>new ActivitySource("qyl.mcp")</c>)
    ///     so the spans land on the same trace as the host's other telemetry.
    /// </param>
    /// <param name="configure">Optional PII-gating override.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    ///     Must be called *after* the transport (<c>WithStdioServerTransport</c> or
    ///     <c>WithHttpTransport</c>) is configured but before tools/resources/prompts
    ///     are registered, so the filter pipeline wraps every primitive the builder
    ///     subsequently adds.
    /// </remarks>
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
                        activity.SetTag(McpAttr.RpcSystem, "jsonrpc");
                        activity.SetTag(McpAttr.RpcMethod, method);
                        activity.SetTag(McpAttr.JsonrpcProtocolVersion, "2.0");
                    }

                    if (context.JsonRpcMessage is JsonRpcRequest req)
                        activity.SetTag(McpAttr.JsonrpcRequestId, req.Id.ToString());

                    SetClientInfo(activity, context);
                }

                try
                {
                    await next(context, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (RecordAndPropagate(activity, ex))
                {
                    throw; // unreachable — RecordAndPropagate returns false
                }
            });

            filters.AddOutgoingFilter(next => async (context, ct) =>
            {
                using var activity = activitySource.StartActivity(
                    "mcp.send",
                    ActivityKind.Client);

                if (activity is not null)
                {
                    activity.SetTag(McpAttr.RpcSystem, "jsonrpc");
                    switch (context.JsonRpcMessage)
                    {
                        case JsonRpcResponse response:
                            activity.SetTag(McpAttr.JsonrpcRequestId, response.Id.ToString());
                            break;
                        case JsonRpcRequest request:
                            activity.SetTag(McpAttr.RpcMethod, request.Method);
                            activity.SetTag(McpAttr.JsonrpcRequestId, request.Id.ToString());
                            break;
                        case JsonRpcNotification notification:
                            activity.SetTag(McpAttr.RpcMethod, notification.Method);
                            break;
                    }
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
                    activity.SetTag(McpAttr.RpcMethod, "tools/call");
                    SetClientInfo(activity, request);

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
                activity?.SetTag(McpAttr.RpcMethod, "resources/read");
                SetClientInfo(activity, request);

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
                activity?.SetTag(McpAttr.RpcMethod, "prompts/get");
                SetClientInfo(activity, request);

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

    private static void SetClientInfo<TParams>(Activity? activity, RequestContext<TParams> ctx)
    {
        if (activity is null) return;
        if (ctx.Server.ClientInfo is { } info)
        {
            activity.SetTag(McpAttr.McpClientName, info.Name);
            activity.SetTag(McpAttr.McpClientVersion, info.Version);
        }
    }

    private static void SetClientInfo(Activity? activity, MessageContext ctx)
    {
        if (activity is null) return;
        if (ctx.Server.ClientInfo is { } info)
        {
            activity.SetTag(McpAttr.McpClientName, info.Name);
            activity.SetTag(McpAttr.McpClientVersion, info.Version);
        }
    }

    // Observe-only exception recorder. Returning false from a `when` filter means the
    // catch clause never runs and the exception propagates with its original stack
    // trace preserved, while still tagging the span with status + recorded exception.
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

/// MCP/JSON-RPC span attribute keys. These are not in the OpenTelemetry SemConv
/// repo yet — the working group is finalising them at <c>mcp.*</c> + <c>rpc.*</c>.
/// Centralising them here keeps the field list one diff away from a future
/// generator pass that emits them from <c>eng/semconv/model/qyl/mcp.yaml</c>.
internal static class McpAttr
{
    public const string RpcSystem = "rpc.system.name";
    public const string RpcMethod = "rpc.method";
    public const string JsonrpcProtocolVersion = "jsonrpc.protocol.version";
    public const string JsonrpcRequestId = "jsonrpc.request.id";

    public const string McpClientName = "mcp.client.name";
    public const string McpClientVersion = "mcp.client.version";

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
