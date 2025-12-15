// =============================================================================
// High-Throughput Span Ingestion Pipeline using Bounded Channels
// =============================================================================

using System.IO.Pipelines;
using System.Threading.Channels;
using qyl.collector.Ingestion;
using qyl.collector.Models;

namespace qyl.collector.Pipeline;

/// <summary>
///     High-throughput span ingestion using bounded channels.
///     Decouples parsing from processing for backpressure handling.
/// </summary>
public sealed class SpanIngestionPipeline : IAsyncDisposable
{
    private readonly Channel<ParsedSpan> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _lock = new();
    private readonly Task _processingTask;
    private readonly Func<ParsedSpan, CancellationToken, ValueTask> _processor;

    public SpanIngestionPipeline(
        Func<ParsedSpan, CancellationToken, ValueTask> processor,
        int capacity = 10_000)
    {
        _processor = processor;
        _channel = Channel.CreateBounded<ParsedSpan>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _processingTask = ProcessSpansAsync(_cts.Token);
    }

    /// <summary>Gets the channel reader for external consumption.</summary>
    public ChannelReader<ParsedSpan> Reader => _channel.Reader;

    /// <summary>Gets the count of spans pending processing.</summary>
    public long PendingCount => _channel.Reader.Count;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _cts.CancelAsync();

        try
        {
            await _processingTask;
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _cts.Dispose();
    }

    /// <summary>
    ///     Ingest OTLP JSON payload. Returns immediately; processing happens asynchronously.
    /// </summary>
    public async ValueTask IngestAsync(ReadOnlyMemory<byte> otlpJson, CancellationToken ct = default)
    {
        var parser = new OtlpJsonSpanParser(otlpJson.Span);
        foreach (var span in parser.ParseExportRequest())
        {
            await _channel.Writer.WriteAsync(span, ct);
        }
    }

    /// <summary>
    ///     Ingest from PipeReader for zero-copy streaming.
    /// </summary>
    public async ValueTask IngestFromPipeAsync(PipeReader reader, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var result = await reader.ReadAsync(ct);
            var buffer = result.Buffer;

            if (buffer.IsEmpty && result.IsCompleted)
                break;

            var parser = new OtlpJsonSpanParser(buffer);
            foreach (var span in parser.ParseExportRequest())
            {
                await _channel.Writer.WriteAsync(span, ct);
            }

            reader.AdvanceTo(buffer.End);
        }
    }

    private async Task ProcessSpansAsync(CancellationToken ct)
    {
        await foreach (var span in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _processor(span, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Log but don't crash the pipeline
                Debug.WriteLine($"Span processing error: {ex.Message}");
            }
        }
    }
}
