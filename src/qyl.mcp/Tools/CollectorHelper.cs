namespace qyl.mcp.Tools;

/// <summary>
///     Shared helper for MCP tool methods that call qyl.collector via HTTP.
/// </summary>
internal static class CollectorHelper
{
    /// <summary>
    ///     Executes an async HTTP operation and returns a standardized error message on failure.
    /// </summary>
    public static async Task<string> ExecuteAsync(
        Func<Task<string>> operation,
        string? errorPrefix = null)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return $"{errorPrefix ?? "Error connecting to qyl collector"}: {ex.Message}";
        }
    }
}
