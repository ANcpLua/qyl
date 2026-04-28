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
using JsonrpcAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Jsonrpc.JsonrpcAttributes;
using RpcAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Rpc.RpcAttributes;

namespace Qyl.Instrumentation.Instrumentation.Mcp;

/// <summary>
///     PII-gated capture controls + transport metadata for the MCP-server filter
///     stack. Mirrors Sentry's <c>recordInputs</c> / <c>recordOutputs</c> + the
///     OTel <c>OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT</c> opt-in shape.
///     Mutable class (not a record) so callers can use the <c>Action&lt;Options&gt;</c>
///     overload of <c>UseQylMcpInstrumentation</c> with familiar property assignment.
/// </summary>
public sealed class QylMcpInstrumentationOptions
{
    /// <summary>
    ///     When <c>true</c>, capture <c>tools/call</c> arguments and <c>prompts/get</c>
    ///     parameters on the span. Off by default — arguments may carry user PII.
    /// </summary>
    public bool RecordInputs { get; set; }

    /// <summary>
    ///     When <c>true</c>, capture <c>tools/call</c> result text, <c>resources/read</c>
    ///     content size, and <c>prompts/get</c> message count on the span. Off by default.
    /// </summary>
    public bool RecordOutputs { get; set; }

    /// <summary>
    ///     Per-attribute character cap when <see cref="RecordInputs" /> or
    ///     <see cref="RecordOutputs" /> is on. Long arguments and results are truncated
    ///     to this length with an ellipsis suffix to stay below collector limits.
    /// </summary>
    public int MaxAttributeValueLength { get; set; } = 4_000;

    /// <summary>
    ///     Transport label tagged on every span as <c>mcp.transport</c>. Used by
    ///     dashboards to group traffic by transport (Sentry's "Transport Distribution"
    ///     widget). Composition roots that know the transport at wire-up time
    ///     (qyl.mcp's <c>McpTransportMode</c> switch, qyl.loom's hard-coded HTTP)
    ///     should set this. Leave <c>null</c> if the transport is decided per-request.
    /// </summary>
    public string? Transport { get; set; }
}

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

    /// Stamps every span with the per-server context: client name/version (Sentry's
    /// "Traffic by Client"), session id (Sentry's "session id grouping"), and the
    /// transport (Sentry's "Transport Distribution"). Pulls all three from the
    /// <see cref="McpServer" /> directly so we don't need separate overloads for
    /// <c>RequestContext&lt;T&gt;</c> vs <c>MessageContext</c>.
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

/// MCP-specific span attribute keys. The OTel working group has not finalised an
/// <c>mcp.*</c> namespace yet, so these are temporary inline constants until
/// <c>eng/semconv/model/qyl/mcp.yaml</c> is added and the weaver pass emits a
/// <c>QylAttr.Mcp</c> generator-output. The standard <c>rpc.*</c> + <c>jsonrpc.*</c>
/// keys are NOT duplicated here — they come from
/// <c>Qyl.OpenTelemetry.SemanticConventions.Incubating</c>'s
/// <c>RpcAttributes</c> + <c>JsonrpcAttributes</c>.
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
