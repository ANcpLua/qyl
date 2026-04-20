namespace qyl.mcp.Formatting;

using System.Net;

/// <summary>
///     Formats errors for LLM consumption with transport-aware detail levels.
///     HTTP: generic (user can't fix server config). Stdio: detailed (dev can fix locally).
/// </summary>
internal static class ErrorFormatter
{
    public static string FormatForLlm(Exception error, McpTransportMode transport) =>
        error switch
        {
            HttpRequestException httpEx => FormatHttpError(httpEx, transport),
            TaskCanceledException { InnerException: TimeoutException or null } =>
                "**Timeout:** The collector did not respond in time. Retry or check if qyl collector is running.",
            OperationCanceledException opEx => FormatOperationCancelled(opEx),
            InvalidOperationException configEx => FormatConfigError(configEx, transport),
            _ => FormatUnknown(error, transport)
        };

    private static string FormatHttpError(HttpRequestException ex, McpTransportMode transport)
    {
        var (category, hint) = ex.StatusCode switch
        {
            HttpStatusCode.NotFound =>
                ("Not Found", "Use the corresponding search tool to find valid IDs."),
            HttpStatusCode.BadRequest =>
                ("Invalid Request", "Check the parameters and try again with corrected values."),
            HttpStatusCode.Unauthorized =>
                ("Authentication Required", transport is McpTransportMode.Stdio
                    ? "Check QYL_MCP_TOKEN or QYL_KEYCLOAK_* environment variables."
                    : "Re-authenticate and try again."),
            HttpStatusCode.Forbidden =>
                ("Access Denied", "This operation requires elevated permissions (qyl:admin role)."),
            >= HttpStatusCode.InternalServerError =>
                ("Collector Error", "The qyl collector encountered an internal error. Try again shortly."),
            _ => ("Connection Error", "Check if the qyl collector is running and reachable.")
        };

        return $"**{category}**\n\n{ex.Message}\n\n{hint}";
    }

    private static string FormatOperationCancelled(OperationCanceledException ex) =>
        ex.Message.ContainsIgnoreCase("tool call limit")
            ? $"**Investigation Budget Reached**\n\n{ex.Message}"
            : "**Cancelled:** The operation was cancelled.";

    private static string FormatConfigError(InvalidOperationException ex, McpTransportMode transport) =>
        transport is McpTransportMode.Stdio
            ? $"**Configuration Error**\n\n{ex.Message}\n\nCheck your environment variables and try again."
            : "**Configuration Error**\n\nThe server has a configuration issue. Contact the administrator.";

    private static string FormatUnknown(Exception ex, McpTransportMode transport) =>
        transport is McpTransportMode.Stdio
            ? $"**Error**\n\n{ex.Message}"
            : "**Error**\n\nAn unexpected error occurred. Try again or contact support.";
}
