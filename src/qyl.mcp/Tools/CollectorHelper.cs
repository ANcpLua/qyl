using System.Net;
using System.Net.Sockets;

namespace qyl.mcp.Tools;

/// <summary>
///     Shared helper for MCP tool methods that call qyl.collector via HTTP.
/// </summary>
internal static class CollectorHelper
{
    /// <summary>
    ///     Executes an async HTTP operation and returns a categorized error message on failure.
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
            string category = ex.StatusCode switch
            {
                HttpStatusCode.NotFound => "Not found",
                HttpStatusCode.BadRequest => "Invalid request",
                HttpStatusCode.Unauthorized => "Authentication required",
                HttpStatusCode.Forbidden => "Access denied",
                >= HttpStatusCode.InternalServerError => "Collector server error",
                _ when ex.InnerException is SocketException => "Connection refused — is qyl collector running?",
                _ when ex.InnerException is TaskCanceledException => "Request timed out",
                _ => "Connection error"
            };
            return $"{errorPrefix ?? category}: {ex.Message}";
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException or null)
        {
            return $"{errorPrefix ?? "Request timed out"}: The collector did not respond in time.";
        }
    }
}
