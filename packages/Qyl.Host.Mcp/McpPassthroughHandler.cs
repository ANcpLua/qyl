using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Qyl.Host;
using ContractBadGatewayError = Qyl.Api.Contracts.Common.Errors.BadGatewayError;
using ContractConflictError = Qyl.Api.Contracts.Common.Errors.ConflictError;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;
using ContractNotFoundError = Qyl.Api.Contracts.Common.Errors.NotFoundError;
using ContractProblemDetailsMediaType = Qyl.Api.Contracts.Common.Errors.ProblemDetailsMediaType;
using ContractValidationError = Qyl.Api.Contracts.Common.Errors.ValidationError;
using ContractValidationErrorDetail = Qyl.Api.Contracts.Common.Errors.ValidationErrorDetail;

namespace Qyl.Host.Mcp;

/// <summary>
/// Loopback runner projection of tools/list, tools/call, and resources/read. The MCP SDK owns
/// both the upstream protocol and these passthrough request and response bodies.
/// </summary>
internal sealed class McpPassthroughHandler(
    McpClientRegistry clients,
    IReadOnlyList<QylResource> resources) : IQylRunnerRequestHandler
{
    private const string Prefix = "/runner/mcp/";
    internal const int MaxRequestBodyBytes = 1024 * 1024;

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
        name = Uri.UnescapeDataString(name);

        if (route is McpRoute.Unknown)
        {
            await RespondNotFoundAsync(context, "runner_mcp_route", path,
                    $"No runner_mcp_route named '{path}' exists. The runner projects an attached " +
                    "MCP server as: GET /runner/mcp/{name}/tools, POST /runner/mcp/{name}/tools/call, " +
                    "POST /runner/mcp/{name}/resources/read.")
                .ConfigureAwait(false);
            return true;
        }

        if (!string.Equals(context.Request.HttpMethod, method, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
            context.Response.Close();
            return true;
        }

        var resource = resources.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.Ordinal));
        if (resource is null)
        {
            await RespondNotFoundAsync(context, "runner_resource", name).ConfigureAwait(false);
            return true;
        }

        if (!clients.TryGet(name, out var client))
        {
            await RespondConflictAsync(context, name, $"Runner resource '{name}' is not ready.")
                .ConfigureAwait(false);
            return true;
        }

        try
        {
            byte[] payload;

            switch (route)
            {
                case McpRoute.ListTools:
                {
                    var result = await client.ListToolsAsync(new ListToolsRequestParams(), cancellationToken)
                        .ConfigureAwait(false);
                    payload = McpSdkJson.Serialize(result);
                    break;
                }
                case McpRoute.CallTool:
                {
                    CallToolRequestParams request;
                    try
                    {
                        request = await ReadBodyAsync(
                                context,
                                McpSdkJson.TypeInfo<CallToolRequestParams>(),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or
                                                   ArgumentException or OverflowException)
                    {
                        throw new InvalidDataException("The MCP tool-call body is invalid.", ex);
                    }

                    var result = await client.CallToolAsync(request, cancellationToken).ConfigureAwait(false);
                    payload = McpSdkJson.Serialize(result);
                    break;
                }
                default:
                {
                    ReadResourceRequestParams request;
                    try
                    {
                        request = await ReadBodyAsync(
                                context,
                                McpSdkJson.TypeInfo<ReadResourceRequestParams>(),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or
                                                   ArgumentException or OverflowException)
                    {
                        throw new InvalidDataException("The MCP resource-read body is invalid.", ex);
                    }

                    var result = await client.ReadResourceAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    payload = McpSdkJson.Serialize(result);
                    break;
                }
            }

            await RespondAsync(context, HttpStatusCode.OK, "application/json", payload).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (InvalidDataException ex)
        {
            await RespondValidationAsync(context, ex.Message).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is McpException or JsonException or IOException or InvalidOperationException or
                                       NotSupportedException or UriFormatException or ArgumentException or OverflowException)
        {
            await RespondBadGatewayAsync(context, name).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            await RespondInternalErrorAsync(context).ConfigureAwait(false);
        }

        return true;
    }

    private static async Task<T> ReadBodyAsync<T>(
        HttpListenerContext context,
        JsonTypeInfo<T> jsonTypeInfo,
        CancellationToken cancellationToken)
    {
        if (context.Request.ContentType is not { } contentType ||
            !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException("Content-Type must be application/json.");
        }

        var boundedBody = await ReadBoundedBodyAsync(
                context.Request.InputStream,
                context.Request.ContentLength64,
                cancellationToken)
            .ConfigureAwait(false);
        await using (boundedBody.ConfigureAwait(false))
        {
            var result = await JsonSerializer.DeserializeAsync(
                    boundedBody,
                    jsonTypeInfo,
                    cancellationToken)
                .ConfigureAwait(false);
            return result ?? throw new JsonException("Request body deserialized to null.");
        }
    }

    internal static async Task<MemoryStream> ReadBoundedBodyAsync(
        Stream input,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        if (declaredLength > MaxRequestBodyBytes)
            throw new InvalidDataException($"Request body exceeds {MaxRequestBodyBytes} bytes.");

        var boundedBody = new MemoryStream(
            declaredLength is > 0 and <= MaxRequestBodyBytes ? (int)declaredLength : 0);
        var buffer = new byte[81920];
        var total = 0;
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read is 0) break;

                total += read;
                if (total > MaxRequestBodyBytes)
                    throw new InvalidDataException($"Request body exceeds {MaxRequestBodyBytes} bytes.");

                await boundedBody.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            boundedBody.Position = 0;
            return boundedBody;
        }
        catch
        {
            boundedBody.Dispose();
            throw;
        }
    }

    private static Task RespondNotFoundAsync(
        HttpListenerContext context, string resourceType, string resourceId, string? detail = null) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.NotFound,
            new ContractNotFoundError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Not Found",
                Status = (int)HttpStatusCode.NotFound,
                Detail = detail ?? $"No {resourceType} named '{resourceId}' exists.",
                ResourceType = resourceType,
                ResourceId = resourceId
            },
            QylMcpProblemJsonContext.Default.NotFoundError);

    private static Task RespondConflictAsync(HttpListenerContext context, string resource, string detail) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.Conflict,
            new ContractConflictError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Conflict",
                Status = (int)HttpStatusCode.Conflict,
                Detail = detail,
                ConflictingResource = resource
            },
            QylMcpProblemJsonContext.Default.ConflictError);

    private static Task RespondValidationAsync(HttpListenerContext context, string detail) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.BadRequest,
            new ContractValidationError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Validation Failed",
                Status = (int)HttpStatusCode.BadRequest,
                Detail = detail,
                Errors =
                [
                    new ContractValidationErrorDetail
                    {
                        Field = "body",
                        Message = detail,
                        Code = "request.invalid"
                    }
                ]
            },
            QylMcpProblemJsonContext.Default.ValidationError);

    private static Task RespondBadGatewayAsync(HttpListenerContext context, string resource) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.BadGateway,
            new ContractBadGatewayError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Bad Gateway",
                Status = (int)HttpStatusCode.BadGateway,
                Detail = $"Runner resource '{resource}' did not complete the MCP request.",
                Dependency = resource
            },
            QylMcpProblemJsonContext.Default.BadGatewayError);

    private static Task RespondInternalErrorAsync(HttpListenerContext context) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.InternalServerError,
            new ContractInternalServerError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Internal Server Error",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "The runner could not complete the MCP request.",
                ErrorCode = "runner.mcp_failed"
            },
            QylMcpProblemJsonContext.Default.InternalServerError);

    private static async Task RespondProblemAsync<T>(
        HttpListenerContext context,
        HttpStatusCode status,
        T problem,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(problem, jsonTypeInfo);
        await RespondAsync(context, status, ContractProblemDetailsMediaType.Value, payload).ConfigureAwait(false);
    }

    private static async Task RespondAsync(
        HttpListenerContext context,
        HttpStatusCode status,
        string contentType,
        byte[] payload)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = contentType;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
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

internal static class McpSdkJson
{
    internal static JsonTypeInfo<T> TypeInfo<T>() =>
        McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
        ?? throw new NotSupportedException($"The MCP SDK JSON context does not include {typeof(T).FullName}.");

    internal static byte[] Serialize<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, TypeInfo<T>());
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ContractNotFoundError))]
[JsonSerializable(typeof(ContractConflictError))]
[JsonSerializable(typeof(ContractValidationError))]
[JsonSerializable(typeof(ContractBadGatewayError))]
[JsonSerializable(typeof(ContractInternalServerError))]
internal sealed partial class QylMcpProblemJsonContext : JsonSerializerContext;
