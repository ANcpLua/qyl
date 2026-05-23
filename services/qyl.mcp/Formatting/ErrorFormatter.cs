using System.Net;

namespace qyl.mcp.Formatting;

internal static class ErrorFormatter
{
    public static string FormatForLlm(Exception error, McpTransportMode transport) =>
        error switch
        {
            HttpRequestException httpEx => FormatHttpError(httpEx, transport),
            TaskCanceledException taskEx when taskEx.CancellationToken.IsCancellationRequested =>
                FormatOperationCancelled(taskEx),
            TaskCanceledException { InnerException: TimeoutException or null } =>
                "**Timeout:** The collector did not respond in time. Retry or check if qyl collector is running.",
            OperationCanceledException opEx => FormatOperationCancelled(opEx),
            // ModelContextProtocol 1.3 throws IOException (incl. ClientTransportClosedException)
            // for transport connect/closure failures that 1.2 wrapped as InvalidOperationException.
            // This arm must precede InvalidOperationException — order matters in C# pattern switches.
            IOException ioEx => FormatTransportError(ioEx, transport),
            InvalidOperationException configEx => FormatConfigError(configEx, transport),
            _ => FormatUnknown(error, transport)
        };

    private static string FormatTransportError(IOException ex, McpTransportMode transport) =>
        transport is McpTransportMode.Stdio
            ? $"**Connection Error**\n\n{ex.Message}\n\nCheck if the MCP server process is running."
            : $"**Connection Error**\n\n{ex.Message}\n\nCheck the endpoint URL and network reachability.";

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
