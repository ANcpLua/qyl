
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

internal sealed partial class LspClientTransport : IAsyncDisposable
{
    private const string HeaderPrefix = "Content-Length: ";
    private static readonly byte[] s_headerTerminator = "\r\n\r\n"u8.ToArray();
    private readonly Channel<JsonObject> _incoming;
    private readonly ILogger? _logger;
    private readonly CancellationTokenSource _readerCts = new();
    private readonly Task _readerTask;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private readonly Stream _writer;
    private int _disposed;
    private long _nextRequestId;

    public LspClientTransport(Stream input, Stream output, ILogger? logger = null)
    {
        _writer = output;
        _logger = logger;
        _incoming = Channel.CreateUnbounded<JsonObject>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = true
        });

        _readerTask = Task.Run(() => ReadLoopAsync(input, _readerCts.Token));
    }

    public ChannelReader<JsonObject> IncomingReader => _incoming.Reader;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) is not 0)
            return;

        await _readerCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await _readerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException cancelled)
        {
            if (_logger is not null) LogReaderLoopCancelled(_logger, cancelled);
        }
        catch (IOException ex)
        {
            if (_logger is not null) LogReaderLoopIoError(_logger, ex);
        }

        _readerCts.Dispose();
        _writeLock.Dispose();
        await _writer.DisposeAsync().ConfigureAwait(false);
    }

    public long AllocateRequestId() => Interlocked.Increment(ref _nextRequestId);

    public Task SendRequestAsync(long id, string method, JsonNode? parameters, CancellationToken ct)
    {
        var frame = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
        if (parameters is not null)
            frame["params"] = parameters;
        return WriteFrameAsync(frame, ct);
    }

    public Task SendNotificationAsync(string method, JsonNode? parameters, CancellationToken ct)
    {
        var frame = new JsonObject { ["jsonrpc"] = "2.0", ["method"] = method };
        if (parameters is not null)
            frame["params"] = parameters;
        return WriteFrameAsync(frame, ct);
    }

    private async Task WriteFrameAsync(JsonObject frame, CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(frame);
        var header = Encoding.ASCII.GetBytes($"{HeaderPrefix}{body.Length}\r\n\r\n");

        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteAsync(header, ct).ConfigureAwait(false);
            await _writer.WriteAsync(body, ct).ConfigureAwait(false);
            await _writer.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(Stream input, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = await ReadFrameAsync(input, ct).ConfigureAwait(false);
                if (frame is null)
                    break;

                await _incoming.Writer.WriteAsync(frame, ct).ConfigureAwait(false);
            }

            _incoming.Writer.TryComplete();
        }
        catch (OperationCanceledException)
        {
            _incoming.Writer.TryComplete();
        }
        catch (EndOfStreamException truncated)
        {
            _incoming.Writer.TryComplete(truncated);
        }
        catch (IOException ioError)
        {
            _incoming.Writer.TryComplete(ioError);
        }
        catch (InvalidDataException frameError)
        {
            _incoming.Writer.TryComplete(frameError);
        }
        catch (JsonException jsonError)
        {
            _incoming.Writer.TryComplete(jsonError);
        }
    }

    private static async Task<JsonObject?> ReadFrameAsync(Stream input, CancellationToken ct)
    {
        var contentLength = await ReadHeadersAsync(input, ct).ConfigureAwait(false);
        if (contentLength < 0)
            return null;

        var rented = ArrayPool<byte>.Shared.Rent(contentLength);
        try
        {
            var body = rented.AsMemory(0, contentLength);
            await ReadExactAsync(input, body, ct).ConfigureAwait(false);

            using var document = JsonDocument.Parse(body);
            return JsonNode.Parse(document.RootElement.GetRawText()) as JsonObject;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static async Task<int> ReadHeadersAsync(Stream input, CancellationToken ct)
    {
        var buffer = new List<byte>(64);
        var scratch = new byte[1];
        var matchIndex = 0;

        while (true)
        {
            var read = await input.ReadAsync(scratch.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (read is 0)
            {
                return buffer.Count is 0
                    ? -1
                    : throw new EndOfStreamException("LSP transport: stream ended inside header.");
            }

            buffer.Add(scratch[0]);

            if (scratch[0] == s_headerTerminator[matchIndex])
            {
                matchIndex++;
                if (matchIndex == s_headerTerminator.Length)
                    break;
            }
            else
            {
                matchIndex = scratch[0] == s_headerTerminator[0] ? 1 : 0;
            }
        }

        var headerText = Encoding.ASCII.GetString(
            buffer.ToArray(), 0, buffer.Count - s_headerTerminator.Length);

        foreach (var line in headerText.Split("\r\n"))
        {
            if (!line.StartsWithIgnoreCase(HeaderPrefix))
                continue;
            if (int.TryParse(line.AsSpan(HeaderPrefix.Length).Trim(), out var length))
                return length;
        }

        throw new InvalidDataException("LSP transport: frame received without Content-Length header.");
    }

    private static async Task ReadExactAsync(Stream input, Memory<byte> destination, CancellationToken ct)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await input.ReadAsync(destination[offset..], ct).ConfigureAwait(false);
            if (read is 0)
            {
                throw new EndOfStreamException(
                    $"LSP transport: stream ended with {destination.Length - offset} bytes still expected.");
            }

            offset += read;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "LSP transport reader loop cancelled during disposal.")]
    private static partial void LogReaderLoopCancelled(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "LSP transport reader loop terminated with IO exception during disposal.")]
    private static partial void LogReaderLoopIoError(ILogger logger, Exception ex);
}
