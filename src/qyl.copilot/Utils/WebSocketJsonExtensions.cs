using System.Net.WebSockets;
using System.Text.Json;

namespace qyl.copilot.Utils;

/// <summary>
///     Extension methods for sending and receiving JSON messages over <see cref="WebSocket" />
///     using <see cref="WebSocketStream" /> (.NET 10).
/// </summary>
public static class WebSocketJsonExtensions
{
    /// <summary>
    ///     Serializes <paramref name="value" /> as JSON and sends it as a single WebSocket message.
    /// </summary>
    public static async Task SendJsonMessageAsync<T>(
        this WebSocket socket,
        T value,
        JsonSerializerOptions? options = null,
        WebSocketMessageType messageType = WebSocketMessageType.Text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        await using var stream = WebSocketStream.CreateWritableMessageStream(socket, messageType);

        await JsonSerializer.SerializeAsync(
            stream,
            value,
            options ?? JsonStreamingExtensions.StrictCamelCase,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Reads a single WebSocket message and deserializes it from JSON.
    /// </summary>
    public static async ValueTask<T?> ReadJsonMessageAsync<T>(
        this WebSocket socket,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(socket);

        await using var stream = WebSocketStream.CreateReadableMessageStream(socket);

        return await JsonSerializer.DeserializeAsync<T>(
            stream,
            options ?? JsonStreamingExtensions.StrictCamelCase,
            cancellationToken).ConfigureAwait(false);
    }
}
