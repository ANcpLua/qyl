using System.IO.Pipelines;
using System.Text.Json;

namespace qyl.copilot.Utils;

/// <summary>
///     Extension methods for streaming JSON arrays through <see cref="PipeWriter" /> and <see cref="PipeReader" />.
///     Uses <see cref="JsonSerializerOptions.Strict" /> with camelCase as the default baseline.
/// </summary>
public static class JsonStreamingExtensions
{
    /// <summary>
    ///     Strict camelCase JSON options suitable for infrastructure code.
    /// </summary>
    public static JsonSerializerOptions StrictCamelCase { get; } = new(JsonSerializerOptions.Strict)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    ///     Serializes an <see cref="IAsyncEnumerable{T}" /> as a JSON array into a <see cref="PipeWriter" />.
    /// </summary>
    public static Task WriteJsonArrayAsync<T>(
        this PipeWriter writer,
        IAsyncEnumerable<T> source,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(source);

        return JsonSerializer.SerializeAsync(
            writer,
            source,
            options ?? StrictCamelCase,
            cancellationToken);
    }

    /// <summary>
    ///     Deserializes a JSON array from a <see cref="PipeReader" /> as an <see cref="IAsyncEnumerable{T}" />.
    /// </summary>
    public static IAsyncEnumerable<T?> ReadJsonArrayAsync<T>(
        this PipeReader reader,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return JsonSerializer.DeserializeAsyncEnumerable<T>(
            reader,
            options ?? StrictCamelCase,
            cancellationToken: cancellationToken);
    }
}
