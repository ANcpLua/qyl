using System.Text;
using Google.Protobuf;
using RpcStatus = Google.Rpc.Status;

namespace Qyl.Collector.Ingestion;

internal enum OtlpPayloadEncoding
{
    Protobuf,
    Json
}

/// <summary>
/// Writes the official OTLP protobuf response envelope in the request's encoding.
/// </summary>
internal sealed class OtlpHttpResult : IResult
{
    private readonly int _statusCode;
    private readonly OtlpPayloadEncoding _encoding;
    private readonly IMessage _message;

    private OtlpHttpResult(int statusCode, OtlpPayloadEncoding encoding, IMessage message)
    {
        _statusCode = statusCode;
        _encoding = encoding;
        _message = message;
    }

    public static IResult Success(OtlpPayloadEncoding encoding, IMessage response) =>
        new OtlpHttpResult(StatusCodes.Status200OK, encoding, response);

    public static IResult Failure(int statusCode, OtlpPayloadEncoding encoding, string message) =>
        new OtlpHttpResult(statusCode, encoding, new RpcStatus { Message = message });

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var statusCode = _statusCode;
        var message = _message;

        // The endpoint handlers answer every decode failure with 400. When the parser refused the body
        // for its size, OTLP 1.11 requires 413 Content Too Large instead; the parser recorded that on
        // the request, so the failure is rewritten here rather than in the handlers.
        if (statusCode is StatusCodes.Status400BadRequest &&
            httpContext.Features.Get<OtlpPayloadTooLargeFeature>() is { } tooLarge)
        {
            statusCode = StatusCodes.Status413PayloadTooLarge;
            message = new RpcStatus { Message = tooLarge.Message };
        }

        httpContext.Response.StatusCode = statusCode;

        if (_encoding is OtlpPayloadEncoding.Protobuf)
        {
            httpContext.Response.ContentType = OtlpPayloadParser.ProtobufContentType;
            var payload = message.ToByteArray();
            httpContext.Response.ContentLength = payload.Length;
            await httpContext.Response.Body.WriteAsync(payload, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        httpContext.Response.ContentType = OtlpPayloadParser.JsonContentType;
        var json = JsonFormatter.Default.Format(message);
        var jsonPayload = Encoding.UTF8.GetBytes(json);
        httpContext.Response.ContentLength = jsonPayload.Length;
        await httpContext.Response.Body.WriteAsync(jsonPayload, httpContext.RequestAborted).ConfigureAwait(false);
    }
}

internal sealed class OtlpUnsupportedMediaTypeException(string? contentType)
    : NotSupportedException($"Unsupported OTLP Content-Type '{contentType ?? "<missing>"}'.")
{
}

internal sealed class OtlpUnsupportedContentEncodingException(string? contentEncoding)
    : NotSupportedException($"Unsupported OTLP Content-Encoding '{contentEncoding ?? "<missing>"}'.")
{
}

/// <summary>
/// The OTLP/HTTP body exceeded the decompressed size bound. OTLP 1.11 (request size limits) requires
/// 413 Content Too Large for this case; the parser records <see cref="OtlpPayloadTooLargeFeature"/>
/// on the request so <see cref="OtlpHttpResult"/> answers 413 even though the endpoint handlers map
/// every parser failure to 400.
/// </summary>
internal sealed class OtlpPayloadTooLargeException(long maxPayloadBytes)
    : IOException(OtlpPayloadTooLargeFeature.MessageFor(maxPayloadBytes))
{
    public long MaxPayloadBytes { get; } = maxPayloadBytes;
}

/// <summary>Request feature: the OTLP body was refused for its size; the response must be 413.</summary>
internal sealed class OtlpPayloadTooLargeFeature(long maxPayloadBytes)
{
    public long MaxPayloadBytes { get; } = maxPayloadBytes;

    public string Message => MessageFor(MaxPayloadBytes);

    internal static string MessageFor(long maxPayloadBytes) =>
        $"OTLP payload exceeds the {maxPayloadBytes / (1024 * 1024)} MiB limit after decompression.";
}
