namespace qyl.mcp.Sentry;

/// <summary>
///     Shared helper for MCP tool methods that call the Sentry REST API.
///     Mirrors <c>CollectorHelper</c> for qyl.collector HTTP calls.
/// </summary>
internal static class qylHelper
{
    public static async Task<string> ExecuteAsync(
        Func<Task<string>> operation,
        string? errorPrefix = null)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return "Sentry auth failed — check SENTRY_AUTH_TOKEN has the required scopes (org:read, event:read, project:read).";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            return "Sentry returned 403 Forbidden — the token may lack required scopes.";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return "Sentry resource not found — check org/project slug and that the resource exists.";
        }
        catch (HttpRequestException ex)
        {
            return $"{errorPrefix ?? "Error contacting Sentry"}: {ex.Message}";
        }
    }
}
