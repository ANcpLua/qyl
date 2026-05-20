using System.Buffers.Binary;
using System.Text.Json;

namespace Qyl.AdGuard.Companion.Messaging;

internal sealed class NativeMessagingProtocol(
    Stream input,
    Stream output,
    TextWriter diagnostics)
{
    private const int HeaderLength = 4;
    private const int MaxRequestBytes = 64 * 1024 * 1024;
    private const int MaxResponseBytes = 1024 * 1024;

    public async Task RunAsync(NativeMessageDispatcher dispatcher, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NativeRequest? request;
            try
            {
                request = await ReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                await diagnostics.WriteLineAsync($"qyl-adguard-companion protocol error: {ex.Message}")
                    .ConfigureAwait(false);
                return;
            }

            if (request is null)
                return;

            var response = await dispatcher.DispatchAsync(request, cancellationToken).ConfigureAwait(false);
            await WriteAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<NativeRequest?> ReadAsync(CancellationToken cancellationToken)
    {
        var header = new byte[HeaderLength];
        var read = await ReadSomeAsync(header, cancellationToken).ConfigureAwait(false);
        if (read is 0)
            return null;

        while (read < HeaderLength)
        {
            var next = await input.ReadAsync(header.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (next is 0)
                throw new InvalidDataException("Native message header ended before 4 bytes.");
            read += next;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxRequestBytes)
            throw new InvalidDataException($"Native message length {length} is outside the allowed range.");

        var payload = new byte[length];
        await input.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);

        return JsonSerializer.Deserialize(payload, CompanionJsonContext.Default.NativeRequest)
               ?? throw new InvalidDataException("Native message payload was not a request object.");
    }

    private async Task WriteAsync(NativeResponse response, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, CompanionJsonContext.Default.NativeResponse);
        if (payload.Length > MaxResponseBytes)
        {
            var fallback = NativeResponse.Fail(
                response.Id,
                "response_too_large",
                $"Native messaging response exceeded {MaxResponseBytes} bytes.");
            payload = JsonSerializer.SerializeToUtf8Bytes(fallback, CompanionJsonContext.Default.NativeResponse);
        }

        var header = new byte[HeaderLength];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await output.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await output.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ReadSomeAsync(byte[] buffer, CancellationToken cancellationToken) =>
        await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

}
