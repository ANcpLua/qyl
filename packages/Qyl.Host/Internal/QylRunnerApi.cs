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
using ContractServiceUnavailableError = Qyl.Api.Contracts.Common.Errors.ServiceUnavailableError;
using ContractLogLine = Qyl.Api.Contracts.Runner.RunnerLogLine;
using ContractResourceState = Qyl.Api.Contracts.Runner.RunnerResourceState;

namespace Qyl.Host.Internal;

// Loopback, Host, origin, and content-type checks protect mutable routes from rebinding and cross-site POSTs.
internal sealed partial class QylRunnerApi(
    QylResourceRegistry registry,
    QylLogStore logStore,
    QylResourceActions resourceActions,
    QylAppOptions options,
    IEnumerable<IQylRunnerRequestHandler> requestHandlers,
    ILogger<QylRunnerApi> logger) : BackgroundService
{
    internal const int MaxConcurrentRequests = 32;
    internal const int MaxConcurrentStreams = 8;

    private int _activeRequests;
    private int _activeStreams;

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

            var isStream = IsStreamingRequest(context.Request);
            if (!TryAcquireCapacity(isStream))
            {
                try
                {
                    await RespondServiceUnavailableAsync(
                            context,
                            isStream
                                ? "runner.stream_capacity"
                                : "runner.request_capacity")
                        .ConfigureAwait(false);
                }
                catch (Exception)
                {
                    AbortResponse(context);
                }

                continue;
            }

            handlers.Add(HandleWithCapacityLeaseAsync(context, isStream, stoppingToken));
        }

        await Task.WhenAll(handlers).ConfigureAwait(false);
    }

    private async Task HandleWithCapacityLeaseAsync(
        HttpListenerContext context,
        bool isStream,
        CancellationToken stoppingToken)
    {
        try
        {
            await HandleAsync(context, stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            if (isStream) Interlocked.Decrement(ref _activeStreams);
            Interlocked.Decrement(ref _activeRequests);
        }
    }

    private bool TryAcquireCapacity(bool isStream)
    {
        if (!TryIncrementBounded(ref _activeRequests, MaxConcurrentRequests)) return false;
        if (!isStream || TryIncrementBounded(ref _activeStreams, MaxConcurrentStreams)) return true;

        Interlocked.Decrement(ref _activeRequests);
        return false;
    }

    private static bool TryIncrementBounded(ref int value, int maximum)
    {
        while (true)
        {
            var current = Volatile.Read(ref value);
            if (current >= maximum) return false;
            if (Interlocked.CompareExchange(ref value, current + 1, current) == current) return true;
        }
    }

    private static bool IsStreamingRequest(HttpListenerRequest request)
    {
        var path = (request.Url?.AbsolutePath ?? string.Empty).TrimEnd('/');
        const string resourcesRoot = QylConstants.Routes.Runner + "/resources";
        return string.Equals(path, resourcesRoot + "/stream", StringComparison.Ordinal) ||
               path.EndsWith("/logs/stream", StringComparison.Ordinal);
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
            // The response is already unusable.
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

        var lastSequence = ParseLastEventId(context.Request.Headers["Last-Event-ID"]);
        using var subscription = logStore.Subscribe(resource);
        var stream = context.Response.OutputStream;

        try
        {
            foreach (var line in logStore.SnapshotAfter(resource, lastSequence))
            {
                var contract = QylRunnerContractMapper.ToContract(line);
                await WriteFrameAsync(stream,
                    JsonSerializer.Serialize(contract, QylRunnerJsonContext.Default.RunnerLogLine), line.Sequence,
                    stoppingToken)
                    .ConfigureAwait(false);
                lastSequence = line.Sequence;
            }

            await foreach (var line in subscription.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if (line.Sequence <= lastSequence) continue;
                var contract = QylRunnerContractMapper.ToContract(line);
                await WriteFrameAsync(stream,
                    JsonSerializer.Serialize(contract, QylRunnerJsonContext.Default.RunnerLogLine), line.Sequence,
                    stoppingToken)
                    .ConfigureAwait(false);
                lastSequence = line.Sequence;
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpListenerException or IOException)
        {
            // Client disconnect or runner shutdown ends the stream.
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // The connection is already closed.
            }
        }
    }

    private static long ParseLastEventId(string? value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) && sequence >= 0
            ? sequence
            : 0;

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
            // Client disconnect or runner shutdown ends the stream.
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch (Exception)
            {
                // The connection is already closed.
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
            eventId: null, cancellationToken);

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

    private static Task RespondServiceUnavailableAsync(HttpListenerContext context, string reason) =>
        RespondProblemAsync(
            context,
            HttpStatusCode.ServiceUnavailable,
            new ContractServiceUnavailableError
            {
                ProblemType = new Uri("about:blank"),
                Title = "Service Unavailable",
                Status = (int)HttpStatusCode.ServiceUnavailable,
                Detail = "The runner has reached its bounded concurrent request capacity. Retry after a request completes.",
                Reason = reason
            },
            QylRunnerJsonContext.Default.ServiceUnavailableError);

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

    private static async Task WriteFrameAsync(
        Stream stream,
        string json,
        long? eventId,
        CancellationToken cancellationToken)
    {
        // The TypeSpec SSE variants are explicitly named "message"; EventSource still dispatches
        // that standard event through onmessage while the wire remains contract-exact.
        var idLine = eventId.HasValue
            ? $"id: {eventId.Value.ToString(CultureInfo.InvariantCulture)}\n"
            : string.Empty;
        var frame = Encoding.UTF8.GetBytes($"{idLine}event: message\ndata: {json}\n\n");
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
[JsonSerializable(typeof(ContractServiceUnavailableError))]
internal sealed partial class QylRunnerJsonContext : JsonSerializerContext;
