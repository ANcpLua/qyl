using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Qyl.Cli.Codex;

internal interface ICodexControlClient
{
    Task<JsonElement> SteerAsync(
        string threadId,
        string turnId,
        string commandId,
        string input,
        CancellationToken cancellationToken);

    Task<JsonElement> InterruptAsync(
        string threadId,
        string turnId,
        CancellationToken cancellationToken);

    Task<JsonElement> ResumeAsync(
        string threadId,
        string commandId,
        string input,
        CancellationToken cancellationToken);
}

internal sealed class CodexAppServerClient : ICodexControlClient, IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _receiveTask;
    private long _requestId;

    public event Func<JsonElement, ValueTask>? MessageReceived;

    public Task Completion => _receiveTask ?? Task.CompletedTask;

    public async Task ConnectAsync(
        Uri endpoint,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        _socket.Options.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
        await _socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
        _receiveTask = ReceiveLoopAsync(_shutdown.Token);

        await SendRequestAsync(
            "initialize",
            static writer =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("clientInfo");
                writer.WriteStartObject();
                writer.WriteString("name", "qyl-observer");
                writer.WriteString("title", "qyl Observe Graph");
                writer.WriteString("version", BuildVersion.ProductVersion);
                writer.WriteEndObject();
                writer.WritePropertyName("capabilities");
                writer.WriteStartObject();
                writer.WriteBoolean("experimentalApi", true);
                writer.WriteBoolean("requestAttestation", false);
                writer.WriteEndObject();
                writer.WriteEndObject();
            },
            cancellationToken).ConfigureAwait(false);
        await SendNotificationAsync("initialized", cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonElement> SendRequestAsync(
        string method,
        Action<Utf8JsonWriter> writeParams,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _requestId);
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion))
            throw new InvalidOperationException($"Codex request id {id} was already in flight.");

        try
        {
            var payload = WriteMessage(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("method", method);
                writer.WriteNumber("id", id);
                writer.WritePropertyName("params");
                writeParams(writer);
                writer.WriteEndObject();
            });
            await SendAsync(payload, cancellationToken).ConfigureAwait(false);
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    public Task<JsonElement> SteerAsync(
        string threadId,
        string turnId,
        string commandId,
        string input,
        CancellationToken cancellationToken) =>
        SendRequestAsync(
            "turn/steer",
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("threadId", threadId);
                writer.WriteString("expectedTurnId", turnId);
                writer.WriteString("clientUserMessageId", commandId);
                WriteTextInput(writer, input);
                writer.WriteEndObject();
            },
            cancellationToken);

    public Task<JsonElement> InterruptAsync(
        string threadId,
        string turnId,
        CancellationToken cancellationToken) =>
        SendRequestAsync(
            "turn/interrupt",
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("threadId", threadId);
                writer.WriteString("turnId", turnId);
                writer.WriteEndObject();
            },
            cancellationToken);

    public Task<JsonElement> ResumeAsync(
        string threadId,
        string commandId,
        string input,
        CancellationToken cancellationToken) =>
        SendRequestAsync(
            "turn/start",
            writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("threadId", threadId);
                writer.WriteString("clientUserMessageId", commandId);
                WriteTextInput(writer, input);
                writer.WriteEndObject();
            },
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _socket.CloseOutputAsync(
                WebSocketCloseStatus.NormalClosure,
                "qyl observer stopped",
                CancellationToken.None).ConfigureAwait(false);
        }
        await _shutdown.CancelAsync().ConfigureAwait(false);
        if (_receiveTask is not null)
            await _receiveTask.ConfigureAwait(false);
        foreach (var pending in _pending.Values)
            pending.TrySetException(new IOException("Codex app-server connection closed."));
        _socket.Dispose();
        _shutdown.Dispose();
        _sendLock.Dispose();
    }

    private async Task SendNotificationAsync(string method, CancellationToken cancellationToken)
    {
        var payload = WriteMessage(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("method", method);
            writer.WriteEndObject();
        });
        await SendAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendAsync(byte[] payload, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(
                payload,
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buffer = new byte[16 * 1024];
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = new MemoryStream();
                await using var messageScope = message.ConfigureAwait(false);
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (result.MessageType is WebSocketMessageType.Close)
                        return;
                    if (result.MessageType is not WebSocketMessageType.Text)
                        throw new InvalidDataException("Codex app-server sent a non-text WebSocket message.");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                using var document = JsonDocument.Parse(
                    message.GetBuffer().AsMemory(0, checked((int)message.Length)));
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var idElement) &&
                    TryReadRequestId(idElement, out var id) &&
                    !root.TryGetProperty("method", out _))
                {
                    if (_pending.TryGetValue(id, out var completion))
                    {
                        if (root.TryGetProperty("error", out var error))
                        {
                            completion.TrySetException(
                                new CodexAppServerRequestException(error.GetRawText()));
                        }
                        else if (root.TryGetProperty("result", out var response))
                        {
                            completion.TrySetResult(response.Clone());
                        }
                        else
                        {
                            completion.TrySetException(
                                new InvalidDataException(
                                    "Codex app-server response has neither result nor error."));
                        }
                    }
                    continue;
                }

                var handler = MessageReceived;
                if (handler is not null)
                    await handler(root.Clone()).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (WebSocketException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private static bool TryReadRequestId(JsonElement element, out long id)
    {
        if (element.ValueKind is JsonValueKind.Number && element.TryGetInt64(out id))
            return true;
        if (element.ValueKind is JsonValueKind.String &&
            long.TryParse(element.GetString(), CultureInfo.InvariantCulture, out id))
        {
            return true;
        }
        id = 0;
        return false;
    }

    private static void WriteTextInput(Utf8JsonWriter writer, string input)
    {
        writer.WritePropertyName("input");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", input);
        writer.WritePropertyName("text_elements");
        writer.WriteStartArray();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();
    }

    private static byte[] WriteMessage(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            write(writer);
        return stream.ToArray();
    }
}

internal sealed class CodexAppServerRequestException(string message) : Exception(message);
