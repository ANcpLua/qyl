using System.Text.Json;
using qyl.mcp.Formatting;

namespace qyl.mcp.Tools;

internal static class CollectorHelper
{
    public static Task<string> ExecuteAsync(
        Func<Task<string>> operation,
        string? errorPrefix = null) =>
        ExecuteAsync(operation, McpTransportMode.Stdio, errorPrefix);

    public static async Task<string> ExecuteAsync(
        Func<Task<string>> operation,
        McpTransportMode transport,
        string? errorPrefix = null)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return FormatWithPrefix(ex, transport, errorPrefix);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException or null)
        {
            return FormatWithPrefix(ex, transport, errorPrefix);
        }
        catch (OperationCanceledException ex)
        {
            return FormatWithPrefix(ex, transport, errorPrefix);
        }
    }

    public static async Task<string> ReadCollectorErrorMessageAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            return response.ReasonPhrase ?? response.StatusCode.ToString();

        try
        {
            var payload = JsonSerializer.Deserialize(body, CollectorDtosJsonContext.Default.CollectorErrorDto);
            if (!string.IsNullOrWhiteSpace(payload?.Error))
                return payload.Error;
        }
        catch (JsonException)
        {
            return body;
        }

        return body;
    }

    private static string FormatWithPrefix(Exception ex, McpTransportMode transport, string? errorPrefix)
    {
        var formatted = ErrorFormatter.FormatForLlm(ex, transport);
        return errorPrefix is not null ? $"{errorPrefix}: {formatted}" : formatted;
    }
}
