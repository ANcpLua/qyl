using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ContractConflictError = Qyl.Api.Contracts.Common.Errors.ConflictError;
using ContractForbiddenError = Qyl.Api.Contracts.Common.Errors.ForbiddenError;
using ContractInternalServerError = Qyl.Api.Contracts.Common.Errors.InternalServerError;
using ContractNotFoundError = Qyl.Api.Contracts.Common.Errors.NotFoundError;
using ContractProblemDetailsMediaType = Qyl.Api.Contracts.Common.Errors.ProblemDetailsMediaType;
using ContractLogLine = Qyl.Api.Contracts.Runner.RunnerLogLine;
using ContractResourceState = Qyl.Api.Contracts.Runner.RunnerResourceState;

namespace Qyl.Host.Internal;

// Loopback-only HTTP surface exposing runner state and acknowledged resource actions to first-party consoles.
// Deliberate choices, all traceable to the design constraints:
//   - HttpListener (pure BCL) not Kestrel  -> AOT-clean, zero added dependency, no builder restructuring.
//   - loopback + Host/origin/content-type validation -> mutable routes reject remote peers,
//                                                        DNS rebinding, and cross-site form POSTs.
//   - source-generated JSON                -> AOT/trim-safe serialization.
// Binding failure is non-fatal: the Spectre TUI remains the primary control surface.
internal sealed partial class QylRunnerApi(
    QylResourceRegistry registry,
    QylLogStore logStore,
    QylResourceActions resourceActions,
    QylAppOptions options,
    IEnumerable<IQylRunnerRequestHandler> requestHandlers,
    ILogger<QylRunnerApi> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var prefix = $"{QylConstants.Network.HttpScheme}://{QylConstants.Network.Loopback}:{options.RunnerPort}{QylConstants.Routes.Runner}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            LogBindFailed(prefix, ex.Message);
            return;
        }

        LogListening(prefix);
        await using var stopRegistration = stoppingToken.Register(listener.Stop).ConfigureAwait(false);

        // Each accepted connection is handled concurrently (SSE streams are long-lived, so we cannot await
        // inline). Handler tasks are tracked — never discarded — and drained on shutdown; HandleAsync is
        // non-throwing, so a tracked task never faults.
        var handlers = new List<Task>();
        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }

            handlers.RemoveAll(static task => task.IsCompleted);
            handlers.Add(HandleAsync(context, stoppingToken));
        }

        await Task.WhenAll(handlers).ConfigureAwait(false);
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken stoppingToken)
    {
        try
        {
            // The runner includes opt-in mutable handlers (MCP tool calls), so local binding alone is
            // insufficient: reject non-loopback peers and untrusted Host values before dispatch. The
            // console uses a same-origin Vite proxy and does not need CORS.
            if (!IsTrustedLocalRequest(context.Request))
            {
                await RespondForbiddenAsync(context,
                    "Runner control is available only through the loopback origin.").ConfigureAwait(false);
                return;
            }

            if (!IsSafeMethod(context.Request.HttpMethod) && !IsTrustedMutationRequest(context.Request))
            {
                await RespondForbiddenAsync(context,
                    "Mutable runner requests must be same-origin and either bodyless or application/json.")
                    .ConfigureAwait(false);
                return;
            }

            // Registered extension handlers get first claim on the request (they may accept verbs the
            // core routes never will). A handler that returns true has written and closed the response.
            foreach (var handler in requestHandlers)
            {
                if (await handler.TryHandleAsync(context, stoppingToken).ConfigureAwait(false)) return;
            }

            var path = (context.Request.Url?.AbsolutePath ?? string.Empty).TrimEnd('/');
            const string resourcesRoot = QylConstants.Routes.Runner + "/resources";
            if (!path.StartsWith(resourcesRoot, StringComparison.Ordinal))
            {
                NotFound(context);
                return;
            }

            var rest = path[resourcesRoot.Length..].Trim('/');
            switch (rest)
            {
                case "":
                    if (!RequireMethod(context, "GET")) return;
                    await SnapshotAsync(context).ConfigureAwait(false);
                    return;
                case "stream":
                    if (!RequireMethod(context, "GET")) return;
                    await StreamAsync(context, stoppingToken).ConfigureAwait(false);
                    return;
            }

            var segments = rest.Split('/');
            if (segments is [var resourceName, "logs"])
            {
                if (!RequireMethod(context, "GET")) return;
                await LogSnapshotAsync(context, DecodePathSegment(resourceName)).ConfigureAwait(false);
            }
            else if (segments is [var streamResource, "logs", "stream"])
            {
                if (!RequireMethod(context, "GET")) return;
                await LogStreamAsync(context, DecodePathSegment(streamResource), stoppingToken).ConfigureAwait(false);
            }
            else if (segments is [var restartResource, "restart"])
            {
                if (!RequireMethod(context, "POST")) return;
                await ResourceActionAsync(
                    context, DecodePathSegment(restartResource), QylResourceAction.Restart, stoppingToken)
                    .ConfigureAwait(false);
            }
            else if (segments is [var stopResource, "stop"])
            {
                if (!RequireMethod(context, "POST")) return;
                await ResourceActionAsync(
                    context, DecodePathSegment(stopResource), QylResourceAction.Stop, stoppingToken)
                    .ConfigureAwait(false);
            }
            else
            {
                NotFound(context);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // runner shutting down — nothing to report
            AbortResponse(context);
        }
        catch (Exception ex)
        {
            LogRequestFailed(ex.Message);
            try
            {
                // Handlers can fail before producing a response. Honor the generated TypeSpec 500
                // contract whenever the connection is still writable; if headers/body already went
                // out (notably SSE) or the client disconnected, this write fails and abort is the
                // only honest outcome.
                await RespondInternalErrorAsync(
                        context,
                        detail: null,
                        errorCode: "runner.unhandled_exception")
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                AbortResponse(context);
            }
        }
    }

    private static void AbortResponse(HttpListenerContext context)
    {
        try
        {
            context.Response.Abort();
        }
        catch (Exception)
        {
            // response already gone
        }
    }

    internal static bool IsTrustedLocalRequest(HttpListenerRequest request)
    {
        if (request.RemoteEndPoint is not { Address: var remote } || !IPAddress.IsLoopback(remote)) return false;

        var host = request.Url?.Host;
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    internal static bool IsTrustedMutationRequest(HttpListenerRequest request)
    {
        var mediaType = request.ContentType?.Split(';', 2)[0].Trim();
        var isJson = string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
        if ((request.HasEntityBody || !string.IsNullOrWhiteSpace(mediaType)) && !isJson)
            return false;

        if (string.Equals(request.Headers["Sec-Fetch-Site"], "cross-site", StringComparison.OrdinalIgnoreCase))
            return false;

        var originValue = request.Headers["Origin"];
        if (string.IsNullOrWhiteSpace(originValue)) return true;
        if (!Uri.TryCreate(originValue, UriKind.Absolute, out var origin) || request.Url is not { } target)
            return false;

        return string.Equals(origin.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(origin.Host, target.Host, StringComparison.OrdinalIgnoreCase) &&
               origin.Port == target.Port;
    }

    private static bool IsSafeMethod(string method) =>
        string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

    private static bool RequireMethod(HttpListenerContext context, string expected)
    {
        if (string.Equals(context.Request.HttpMethod, expected, StringComparison.OrdinalIgnoreCase)) return true;

        context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
        context.Response.Close();
        return false;
    }

    private static string DecodePathSegment(string value) => Uri.UnescapeDataString(value);

    private async Task ResourceActionAsync(
        HttpListenerContext context,
        string resource,
        QylResourceAction action,
        CancellationToken cancellationToken)
    {
        var result = await resourceActions.RequestAsync(resource, action, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case QylResourceActionStatus.Accepted:
                context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                context.Response.Close();
                return;
            case QylResourceActionStatus.NotFound:
                await RespondNotFoundAsync(context, resource).ConfigureAwait(false);
                return;
            case QylResourceActionStatus.Conflict:
                await RespondConflictAsync(context, resource, result.Reason).ConfigureAwait(false);
                return;
            default:
                await RespondInternalErrorAsync(context, result.Reason).ConfigureAwait(false);
                return;
        }
    }

    private async Task SnapshotAsync(HttpListenerContext context)
    {
        var states = SortedContractSnapshot();
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            states,
            QylRunnerJsonContext.Default.RunnerResourceStateArray);

        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task LogSnapshotAsync(HttpListenerContext context, string resource)
    {
        if (!registry.Contains(resource))
        {
            await RespondNotFoundAsync(context, resource).ConfigureAwait(false);
            return;
        }

        ContractLogLine[] lines = [.. logStore.Snapshot(resource).Select(QylRunnerContractMapper.ToContract)];
        var payload = JsonSerializer.SerializeToUtf8Bytes(
            lines,
            QylRunnerJsonContext.Default.RunnerLogLineArray);

        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task LogStreamAsync(HttpListenerContext context, string resource, CancellationToken stoppingToken)
    {
        if (!registry.Contains(resource))
        {
            await RespondNotFoundAsync(context, resource).ConfigureAwait(false);
            return;
        }

        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.SendChunked = true;

        using var subscription = logStore.Subscribe(resource);
        var stream = context.Response.OutputStream;

        try
        {
            foreach (var line in logStore.Snapshot(resource))
            {
                var contract = QylRunnerContractMapper.ToContract(line);
                await WriteFrameAsync(stream,
                    JsonSerializer.Serialize(contract, QylRunnerJsonContext.Default.RunnerLogLine), stoppingToken)
                    .ConfigureAwait(false);
            }

            await foreach (var line in subscription.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                var contract = QylRunnerContractMapper.ToContract(line);
                await WriteFrameAsync(stream,
                    JsonSerializer.Serialize(contract, QylRunnerJsonContext.Default.RunnerLogLine), stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or IOException)
        {
            // client disconnected or the runner is shutting down — end the stream quietly
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // connection already torn down
            }
        }
    }

    private static void NotFound(HttpListenerContext context)
    {
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.Close();
    }

    private async Task StreamAsync(HttpListenerContext context, CancellationToken stoppingToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.SendChunked = true;

        // Subscribe first, then replay the snapshot: no state change can slip between the two, and a
        // duplicate replay is idempotent because the client keys resources by name (last-write-wins).
        using var subscription = registry.Subscribe();
        var stream = context.Response.OutputStream;

        try
        {
            foreach (var state in SortedContractSnapshot())
            {
                await WriteEventAsync(stream, state, stoppingToken).ConfigureAwait(false);
            }

            await foreach (var _ in subscription.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                foreach (var state in SortedContractSnapshot())
                    await WriteEventAsync(stream, state, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or IOException)
        {
            // client disconnected or the runner is shutting down — end the stream quietly
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // connection already torn down
            }
        }
    }

    private ContractResourceState[] SortedContractSnapshot() =>
    [
        .. registry.Snapshot.Values
            .OrderBy(static state => state.Name, StringComparer.Ordinal)
            .Select(QylRunnerContractMapper.ToContract)
    ];

    private static Task WriteEventAsync(
        Stream stream,
        ContractResourceState state,
        CancellationToken cancellationToken) =>
        WriteFrameAsync(stream, JsonSerializer.Serialize(state, QylRunnerJsonContext.Default.RunnerResourceState),
            cancellationToken);

    private static Task RespondNotFoundAsync(HttpListenerContext context, string resource) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.NotFound,
            new ContractNotFoundError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Not Found",
                Status = (int)HttpStatusCode.NotFound,
                Detail = $"No runner resource named '{resource}' exists.",
                ResourceType = "runner_resource",
                ResourceId = resource
            },
            QylRunnerJsonContext.Default.NotFoundError);

    private static Task RespondForbiddenAsync(HttpListenerContext context, string detail) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.Forbidden,
            new ContractForbiddenError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Forbidden",
                Status = (int)HttpStatusCode.Forbidden,
                Detail = detail,
                RequiredPermission = "same-origin runner control"
            },
            QylRunnerJsonContext.Default.ForbiddenError);

    private static Task RespondConflictAsync(HttpListenerContext context, string resource, string? detail) =>
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
            QylRunnerJsonContext.Default.ConflictError);

    private static Task RespondInternalErrorAsync(
        HttpListenerContext context,
        string? detail,
        string errorCode = "runner.action_failed") =>
        RespondProblemAsync(
            context,
            HttpStatusCode.InternalServerError,
            new ContractInternalServerError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Internal Server Error",
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = detail,
                ErrorCode = errorCode
            },
            QylRunnerJsonContext.Default.InternalServerError);

    private static async Task RespondProblemAsync<T>(
        HttpListenerContext context,
        HttpStatusCode status,
        T problem,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(problem, jsonTypeInfo);
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = ContractProblemDetailsMediaType.Value;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
        context.Response.Close();
    }

    private static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
    {
        // The TypeSpec SSE variants are explicitly named "message"; EventSource still dispatches
        // that standard event through onmessage while the wire remains contract-exact.
        var frame = Encoding.UTF8.GetBytes($"event: message\ndata: {json}\n\n");
        await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = QylConstants.LogEvents.RunnerApiListening, Level = LogLevel.Information,
        Message = "Runner API listening on {Prefix}")]
    private partial void LogListening(string prefix);

    [LoggerMessage(EventId = QylConstants.LogEvents.RunnerApiBindFailed, Level = LogLevel.Warning,
        Message = "Runner API could not bind {Prefix} — runner-console state feed disabled: {Reason}")]
    private partial void LogBindFailed(string prefix, string reason);

    [LoggerMessage(EventId = QylConstants.LogEvents.RunnerApiRequestFailed, Level = LogLevel.Debug,
        Message = "Runner API request failed: {Reason}")]
    private partial void LogRequestFailed(string reason);
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ContractResourceState))]
[JsonSerializable(typeof(ContractResourceState[]))]
[JsonSerializable(typeof(ContractLogLine))]
[JsonSerializable(typeof(ContractLogLine[]))]
[JsonSerializable(typeof(ContractNotFoundError))]
[JsonSerializable(typeof(ContractForbiddenError))]
[JsonSerializable(typeof(ContractConflictError))]
[JsonSerializable(typeof(ContractInternalServerError))]
internal sealed partial class QylRunnerJsonContext : JsonSerializerContext;
