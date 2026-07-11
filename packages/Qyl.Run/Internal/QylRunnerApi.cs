using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Qyl.Run.Internal;

// Read-only, loopback-only HTTP surface exposing the runner's resource state to the dev runner console (packages/Qyl.Run.Console).
// Deliberate choices, all traceable to the design constraints:
//   - HttpListener (pure BCL) not Kestrel  -> AOT-clean, zero added dependency, no builder restructuring.
//   - GET-only, NO control verbs IN CORE   -> the core routes cannot mutate; start/stop/restart stay on
//                                             the TUI keyboard. Opt-in packages may claim additional
//                                             /runner/* routes (with their own verbs) through the
//                                             IQylRunnerRequestHandler seam — e.g. Qyl.Host.Mcp's
//                                             /runner/mcp/* passthrough.
//   - source-generated JSON                -> AOT/trim-safe serialization.
// Binding failure is non-fatal: the Spectre TUI remains the primary control surface.
internal sealed partial class QylRunnerApi(
    QylResourceRegistry registry,
    QylLogStore logStore,
    QylAppOptions options,
    IEnumerable<IQylRunnerRequestHandler> requestHandlers,
    ILogger<QylRunnerApi> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options;
        var prefix = $"{QylConstants.Network.HttpScheme}://{opts.RunnerHost}:{opts.RunnerPort}{QylConstants.Routes.Runner}/";

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
            // Read-only, dev-only, loopback: permit any origin so the runner console (served from Vite/another
            // loopback port) can fetch/subscribe. There is nothing here to protect and nothing to mutate.
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";

            // Registered extension handlers get first claim on the request (they may accept verbs the
            // core routes never will). A handler that returns true has written and closed the response.
            foreach (var handler in requestHandlers)
            {
                if (await handler.TryHandleAsync(context, stoppingToken).ConfigureAwait(false)) return;
            }

            if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                context.Response.Close();
                return;
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
                    await SnapshotAsync(context).ConfigureAwait(false);
                    return;
                case "stream":
                    await StreamAsync(context, stoppingToken).ConfigureAwait(false);
                    return;
            }

            var segments = rest.Split('/');
            if (segments is [var resourceName, "logs"])
            {
                await LogSnapshotAsync(context, resourceName).ConfigureAwait(false);
            }
            else if (segments is [var streamResource, "logs", "stream"])
            {
                await LogStreamAsync(context, streamResource, stoppingToken).ConfigureAwait(false);
            }
            else
            {
                NotFound(context);
            }
        }
        catch (OperationCanceledException)
        {
            // runner shutting down — nothing to report
        }
        catch (Exception ex)
        {
            LogRequestFailed(ex.Message);
            try
            {
                context.Response.Abort();
            }
            catch (Exception)
            {
                // response already gone
            }
        }
    }

    private async Task SnapshotAsync(HttpListenerContext context)
    {
        var states = SortedSnapshot();
        var payload = JsonSerializer.SerializeToUtf8Bytes(states, QylRunnerJsonContext.Default.QylResourceStateArray);

        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task LogSnapshotAsync(HttpListenerContext context, string resource)
    {
        QylLogLine[] lines = [.. logStore.Snapshot(resource)];
        var payload = JsonSerializer.SerializeToUtf8Bytes(lines, QylRunnerJsonContext.Default.QylLogLineArray);

        context.Response.ContentType = "application/json";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = payload.Length;
        await context.Response.OutputStream.WriteAsync(payload).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task LogStreamAsync(HttpListenerContext context, string resource, CancellationToken stoppingToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.SendChunked = true;

        using var subscription = logStore.Subscribe(resource);
        var stream = context.Response.OutputStream;

        try
        {
            foreach (var line in logStore.Snapshot(resource))
            {
                await WriteFrameAsync(stream,
                    JsonSerializer.Serialize(line, QylRunnerJsonContext.Default.QylLogLine), stoppingToken)
                    .ConfigureAwait(false);
            }

            await foreach (var line in subscription.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await WriteFrameAsync(stream,
                    JsonSerializer.Serialize(line, QylRunnerJsonContext.Default.QylLogLine), stoppingToken)
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
            foreach (var state in SortedSnapshot())
            {
                await WriteEventAsync(stream, state, stoppingToken).ConfigureAwait(false);
            }

            await foreach (var state in subscription.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
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

    private QylResourceState[] SortedSnapshot()
    {
        return [.. registry.Snapshot.Values.OrderBy(static s => s.Name, StringComparer.Ordinal)];
    }

    private static Task WriteEventAsync(Stream stream, QylResourceState state, CancellationToken cancellationToken) =>
        WriteFrameAsync(stream, JsonSerializer.Serialize(state, QylRunnerJsonContext.Default.QylResourceState),
            cancellationToken);

    private static async Task WriteFrameAsync(Stream stream, string json, CancellationToken cancellationToken)
    {
        var frame = Encoding.UTF8.GetBytes($"data: {json}\n\n");
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

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(QylResourceState))]
[JsonSerializable(typeof(QylResourceState[]))]
[JsonSerializable(typeof(QylLogLine))]
[JsonSerializable(typeof(QylLogLine[]))]
internal sealed partial class QylRunnerJsonContext : JsonSerializerContext;
