using StatusCode = Grpc.Core.StatusCode;

namespace qyl.collector.Grpc;

/// <summary>
///     gRPC implementation of the OTLP TraceService for span ingestion on port 4317.
///     Uses OtlpConverter for conversion (shared with HTTP endpoint).
/// </summary>
public sealed class TraceServiceImpl : TraceServiceBase
{
    private readonly ITelemetrySseBroadcaster _broadcaster;
    private readonly DuckDbStore _store;

    public TraceServiceImpl(DuckDbStore store, ITelemetrySseBroadcaster broadcaster)
    {
        _store = Throw.IfNull(store);
        _broadcaster = Throw.IfNull(broadcaster);
    }

    /// <summary>
    ///     Implements opentelemetry.proto.collector.trace.v1.TraceService.Export
    /// </summary>
    public override async Task<ExportTraceServiceResponse> Export(
        ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var spans = OtlpConverter.ConvertProtoToStorageRows(request);

            if (spans.Count <= 0) return new ExportTraceServiceResponse();
            var batch = new SpanBatch(spans);
            await _store.EnqueueAsync(batch, context.CancellationToken).ConfigureAwait(false);
            _broadcaster.PublishSpans(batch);

            return new ExportTraceServiceResponse();
        }
        catch (OperationCanceledException)
        {
            throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));
        }
        catch (ObjectDisposedException)
        {
            throw new RpcException(new Status(StatusCode.Unavailable, "Service is shutting down"));
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to process trace data: {ex.Message}"));
        }
    }
}

/// <summary>
///     Zero-allocation W3C traceparent header parser.
///     Format: {version}-{trace-id}-{parent-id}-{flags}
///     Example: 00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01
/// </summary>
public static class TraceContextParser
{
    private const int TraceParentLength = 55;
    private const int TraceIdOffset = 3;
    private const int ParentIdOffset = 36;
    private const int FlagsOffset = 53;

    /// <summary>
    ///     Parses a W3C traceparent header from a char span.
    /// </summary>
    public static bool TryParse(
        ReadOnlySpan<char> traceParent,
        out TraceId traceId,
        out SpanId parentId,
        out byte flags)
    {
        traceId = default;
        parentId = default;
        flags = 0;

        if (traceParent.Length < TraceParentLength)
            return false;

        // Version check (only 00 supported)
        if (traceParent[0] != '0' || traceParent[1] != '0' || traceParent[2] != '-')
            return false;

        // Parse trace-id (32 hex chars)
        if (!TraceId.TryParse(traceParent.Slice(TraceIdOffset, 32), null, out traceId))
            return false;

        // Delimiter check
        if (traceParent[35] != '-')
            return false;

        // Parse parent-id (16 hex chars)
        if (!SpanId.TryParse(traceParent.Slice(ParentIdOffset, 16), null, out parentId))
            return false;

        // Delimiter check
        if (traceParent[52] != '-')
            return false;

        // Parse flags (2 hex chars)
        if (!byte.TryParse(traceParent.Slice(FlagsOffset, 2), NumberStyles.HexNumber, null, out flags))
            return false;

        return true;
    }

    /// <summary>
    ///     Parses a W3C traceparent header from a UTF-8 byte span.
    /// </summary>
    public static bool TryParse(
        ReadOnlySpan<byte> traceParentUtf8,
        out TraceId traceId,
        out SpanId parentId,
        out byte flags)
    {
        traceId = default;
        parentId = default;
        flags = 0;

        if (traceParentUtf8.Length < TraceParentLength)
            return false;

        // Version check
        if (traceParentUtf8[0] != '0' || traceParentUtf8[1] != '0' || traceParentUtf8[2] != '-')
            return false;

        // Parse trace-id
        if (!TraceId.TryParse(traceParentUtf8.Slice(TraceIdOffset, 32), null, out traceId))
            return false;

        if (traceParentUtf8[35] != '-')
            return false;

        // Parse parent-id
        if (!SpanId.TryParse(traceParentUtf8.Slice(ParentIdOffset, 16), null, out parentId))
            return false;

        if (traceParentUtf8[52] != '-')
            return false;

        // Parse flags
        Span<byte> flagBytes = stackalloc byte[1];
        if (Convert.FromHexString(traceParentUtf8.Slice(FlagsOffset, 2), flagBytes, out _, out _) !=
            OperationStatus.Done)
            return false;

        flags = flagBytes[0];
        return true;
    }

    /// <summary>
    ///     Formats trace context as a W3C traceparent header.
    /// </summary>
    public static bool TryFormat(
        TraceId traceId,
        SpanId spanId,
        byte flags,
        Span<char> destination)
    {
        if (destination.Length < TraceParentLength)
            return false;

        // Version
        destination[0] = '0';
        destination[1] = '0';
        destination[2] = '-';

        // Trace ID
        if (!traceId.TryFormat(destination.Slice(3, 32), out _, default, null))
            return false;

        destination[35] = '-';

        // Span ID
        var spanStr = spanId.ToString();
        spanStr.AsSpan().CopyTo(destination.Slice(36, 16));

        destination[52] = '-';

        // Flags
        destination[53] = GetHexChar(flags >> 4);
        destination[54] = GetHexChar(flags & 0xF);

        return true;
    }

    /// <summary>
    ///     Creates a traceparent string from trace context.
    /// </summary>
    public static string Format(TraceId traceId, SpanId spanId, byte flags = 0x01)
    {
        Span<char> buffer = stackalloc char[TraceParentLength];
        return TryFormat(traceId, spanId, flags, buffer) ? new string(buffer) : string.Empty;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char GetHexChar(int value) => (char)(value < 10 ? '0' + value : 'a' + value - 10);
}
