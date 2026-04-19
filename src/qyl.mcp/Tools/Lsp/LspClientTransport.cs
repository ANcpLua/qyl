// Copyright (c) 2025-2026 ancplua

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace qyl.mcp.Tools.Lsp;

/// <summary>
///     JSON-RPC 2.0 stdio transport with LSP-style <c>Content-Length</c> framing. Hand-rolled
///     on top of <c>System.Text.Json</c> and <c>System.Threading.Channels</c> — six tools and a
///     dozen methods do not justify the <c>StreamJsonRpc</c> dependency. See the skill's
///     "JSON-RPC framing" rule.
/// </summary>
internal sealed class LspClientTransport : IAsyncDisposable
{
    // LSP base protocol: "Content-Length: N\r\n\r\n" header, UTF-8 JSON body.
    private const string HeaderPrefix = "Content-Length: ";
    private static readonly byte[] HeaderTerminator = "\r\n\r\n"u8.ToArray();

    private readonly Stream _writer;
    private readonly SemaphoreSlim _writeLock = new(initialCount: 1, maxCount: 1);
    private readonly Channel<JsonObject> _incoming;
    private readonly CancellationTokenSource _readerCts = new();
    private readonly Task _readerTask;
    private readonly ILogger? _logger;
    private long _nextRequestId;
    private int _disposed;

    /// <summary>
    ///     Constructs a transport over pre-connected stdio streams and starts a background reader
    ///     that demultiplexes incoming frames into <see cref="IncomingReader" />.
    /// </summary>
    /// <param name="input">The stream to read frames from (the server's stdout).</param>
    /// <param name="output">The stream to write frames to (the server's stdin).</param>
    /// <param name="logger">Optional logger for disposal-time diagnostics.</param>
    public LspClientTransport(Stream input, Stream output, ILogger? logger = null)
    {
        _writer = output;
        _logger = logger;
        _incoming = Channel.CreateUnbounded<JsonObject>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        _readerTask = Task.Run(() => ReadLoopAsync(input, _readerCts.Token));
    }

    /// <summary>Reader side of the demultiplex channel.</summary>
    public ChannelReader<JsonObject> IncomingReader => _incoming.Reader;

    /// <summary>Allocate a monotonic request id for a JSON-RPC <c>id</c> field.</summary>
    public long AllocateRequestId() => Interlocked.Increment(ref _nextRequestId);

    /// <summary>Sends a JSON-RPC request frame (one with an <c>id</c>).</summary>
    public Task SendRequestAsync(long id, string method, JsonNode? parameters, CancellationToken ct)
    {
        var frame = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
        };
        if (parameters is not null)
            frame["params"] = parameters;
        return WriteFrameAsync(frame, ct);
    }

    /// <summary>Sends a JSON-RPC notification frame (no <c>id</c>).</summary>
    public Task SendNotificationAsync(string method, JsonNode? parameters, CancellationToken ct)
    {
        var frame = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
        };
        if (parameters is not null)
            frame["params"] = parameters;
        return WriteFrameAsync(frame, ct);
    }

    private async Task WriteFrameAsync(JsonObject frame, CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(frame);
        var header = Encoding.ASCII.GetBytes($"{HeaderPrefix}{body.Length}\r\n\r\n");

        // Serialize writes so concurrent callers cannot interleave frames.
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
        // IO, parse, or protocol-level errors propagate to the consumer via channel.Complete(ex)
        // so waiting requests fail fast with an accurate cause. We do not swallow.
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
            // Disposal flipped the reader CTS — finish cleanly.
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
        // Accumulate bytes one at a time until we see the "\r\n\r\n" header terminator.
        // Header size is tiny — a few dozen bytes — so byte-at-a-time keeps the code small.
        var buffer = new List<byte>(capacity: 64);
        var scratch = new byte[1];
        var matchIndex = 0;

        while (true)
        {
            var read = await input.ReadAsync(scratch.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (read is 0)
                return buffer.Count is 0 ? -1 : throw new EndOfStreamException("LSP transport: stream ended inside header.");

            buffer.Add(scratch[0]);

            if (scratch[0] == HeaderTerminator[matchIndex])
            {
                matchIndex++;
                if (matchIndex == HeaderTerminator.Length)
                    break;
            }
            else
            {
                matchIndex = scratch[0] == HeaderTerminator[0] ? 1 : 0;
            }
        }

        var headerText = Encoding.ASCII.GetString(
            buffer.ToArray(), 0, buffer.Count - HeaderTerminator.Length);

        foreach (var line in headerText.Split("\r\n", StringSplitOptions.None))
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
                throw new EndOfStreamException(
                    $"LSP transport: stream ended with {destination.Length - offset} bytes still expected.");

            offset += read;
        }
    }

    /// <inheritdoc />
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
            _logger?.LogDebug(cancelled, "LSP transport reader loop cancelled during disposal.");
        }
        catch (IOException ex)
        {
            _logger?.LogDebug(ex, "LSP transport reader loop terminated with IO exception during disposal.");
        }

        _readerCts.Dispose();
        _writeLock.Dispose();
    }
}

