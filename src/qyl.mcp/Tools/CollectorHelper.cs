using qyl.mcp.Formatting;

namespace qyl.mcp.Tools;

/// <summary>
///     Shared helper for MCP tool methods that call qyl.collector via HTTP.
/// </summary>
internal static class CollectorHelper
{
    /// <summary>
    ///     Executes an async HTTP operation and returns a categorized error message on failure.
    ///     Defaults to <see cref="McpTransportMode.Stdio"/> detail level.
    /// </summary>
    public static Task<string> ExecuteAsync(
        Func<Task<string>> operation,
        string? errorPrefix = null) =>
        ExecuteAsync(operation, McpTransportMode.Stdio, errorPrefix);

    /// <summary>
    ///     Executes an async HTTP operation and returns a transport-aware error message on failure.
    /// </summary>
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
    }

    private static string FormatWithPrefix(Exception ex, McpTransportMode transport, string? errorPrefix)
    {
        var formatted = ErrorFormatter.FormatForLlm(ex, transport);
        return errorPrefix is not null ? $"{errorPrefix}: {formatted}" : formatted;
    }
}
