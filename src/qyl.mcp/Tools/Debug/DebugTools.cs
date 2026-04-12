using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Debug;

/// <summary>
///     MCP tools for debugging via Rider's Debugger MCP plugin.
///     Stable contracts — backend will migrate from Rider proxy to native .NET debugging.
/// </summary>
[McpServerToolType]
[QylSkill(QylSkillKind.Debug)]
internal sealed class DebugTools(RiderMcpProxy proxy, JetBrainsDiscovery discovery)
{
    // -- Connection -------------------------------------------------------

    [QylCapability("debugger_control", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.debug.status", Title = "Debug Connection Status",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Check Rider debugger MCP connection status and refresh endpoint discovery.")]
    public string GetStatus(
        [Description("Force re-scan of Rider IDE log for endpoint changes")]
        bool refresh = false)
    {
        if (refresh) discovery.Refresh();
        var endpoints = discovery.GetEndpoints();
        var sb = new StringBuilder();
        sb.AppendLine("# Debug Connection Status");
        sb.AppendLine($"- Proxy: {proxy.GetStatus()}");
        sb.AppendLine($"- Rider SSE: {endpoints?.BuiltInSseUrl ?? "not found"}");
        sb.AppendLine($"- Debugger MCP: {endpoints?.DebuggerStreamableUrl ?? "not found"}");
        return sb.ToString();
    }

    [McpServerTool(Name = "qyl.debug.list_available_tools", Title = "List Debugger Tools",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all tools available from the connected Rider debugger MCP server.")]
    public async Task<string> ListAvailableToolsAsync(CancellationToken ct = default) =>
        await ProxyAsync(async () =>
        {
            var tools = await proxy.ListToolsAsync(ct).ConfigureAwait(false);
            var sb = new StringBuilder();
            sb.AppendLine($"# Rider Debugger Tools ({tools.Count})");
            foreach (var tool in tools)
                sb.AppendLine($"- **{tool.Name}**: {tool.Description?[..Math.Min(tool.Description.Length, 80)]}");
            return sb.ToString();
        });

    // -- Sessions ---------------------------------------------------------

    [QylCapability("debugger_control", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.debug.start_session", Title = "Start Debug Session",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Start a debug session using a run configuration name.")]
    public Task<string> StartDebugSessionAsync(
        [Description("Run configuration name to debug")]
        string configurationName,
        CancellationToken ct = default) =>
        CallRiderToolAsync("start_debug_session",
            new Dictionary<string, object> { ["configurationName"] = configurationName }, ct);

    [McpServerTool(Name = "qyl.debug.stop_session", Title = "Stop Debug Session",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Stop a running debug session.")]
    public Task<string> StopDebugSessionAsync(
        [Description("Debug session ID to stop (omit for active session)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("stop_debug_session", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.list_sessions", Title = "List Debug Sessions",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all active debug sessions.")]
    public Task<string> ListDebugSessionsAsync(CancellationToken ct = default) =>
        CallRiderToolAsync("list_debug_sessions", null, ct);

    [McpServerTool(Name = "qyl.debug.session_status", Title = "Get Debug Session Status",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get detailed status of a debug session.")]
    public Task<string> GetDebugSessionStatusAsync(
        [Description("Debug session ID (omit for active session)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_debug_session_status", BuildArgs(("sessionId", sessionId)), ct);

    // -- Run Configurations -----------------------------------------------

    [QylCapability("debugger_control", QylCapabilityRole.Starting)]
    [McpServerTool(Name = "qyl.debug.list_run_configs", Title = "List Run Configurations",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List available run/debug configurations in the project.")]
    public Task<string> ListRunConfigurationsAsync(CancellationToken ct = default) =>
        CallRiderToolAsync("list_run_configurations", null, ct);

    // -- Breakpoints ------------------------------------------------------

    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.set_breakpoint", Title = "Set Breakpoint",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Set a breakpoint at a file and line number.")]
    public Task<string> SetBreakpointAsync(
        [Description("Absolute file path")] string filePath,
        [Description("Line number (1-based)")] int lineNumber,
        [Description("Optional condition expression")]
        string? condition = null,
        [Description("Optional log message (logpoint)")]
        string? logExpression = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("set_breakpoint", BuildArgs(
            ("filePath", filePath), ("lineNumber", lineNumber),
            ("condition", condition), ("logExpression", logExpression)), ct);

    [McpServerTool(Name = "qyl.debug.remove_breakpoint", Title = "Remove Breakpoint",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Remove a breakpoint by its ID.")]
    public Task<string> RemoveBreakpointAsync(
        [Description("Breakpoint ID")] string breakpointId,
        CancellationToken ct = default) =>
        CallRiderToolAsync("remove_breakpoint", new Dictionary<string, object> { ["breakpointId"] = breakpointId }, ct);

    [McpServerTool(Name = "qyl.debug.list_breakpoints", Title = "List Breakpoints",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all breakpoints in the current project.")]
    public Task<string> ListBreakpointsAsync(CancellationToken ct = default) =>
        CallRiderToolAsync("list_breakpoints", null, ct);

    // -- Execution Control ------------------------------------------------

    [McpServerTool(Name = "qyl.debug.resume", Title = "Resume Execution",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Resume program execution from a breakpoint.")]
    public Task<string> ResumeAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("resume", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.pause", Title = "Pause Execution",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Pause program execution.")]
    public Task<string> PauseAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("pause", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.step_over", Title = "Step Over",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Step over to the next line in the current function.")]
    public Task<string> StepOverAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("step_over", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.step_into", Title = "Step Into",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Step into the next function call.")]
    public Task<string> StepIntoAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("step_into", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.step_out", Title = "Step Out",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Step out of the current function.")]
    public Task<string> StepOutAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("step_out", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.run_to_line", Title = "Run to Line",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Continue execution until a specific line is reached.")]
    public Task<string> RunToLineAsync(
        [Description("Absolute file path")] string filePath,
        [Description("Target line number")] int lineNumber,
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("run_to_line", BuildArgs(
            ("filePath", filePath), ("lineNumber", lineNumber), ("sessionId", sessionId)), ct);

    // -- Inspection -------------------------------------------------------

    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.evaluate", Title = "Evaluate Expression",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Evaluate an expression in the current debug context.")]
    public Task<string> EvaluateAsync(
        [Description("Expression to evaluate")]
        string expression,
        [Description("Stack frame index (0 = top)")]
        int? frameIndex = null,
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("evaluate", BuildArgs(
            ("expression", expression), ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);

    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.get_variables", Title = "Get Variables",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get local variables and their values in the current stack frame.")]
    public Task<string> GetVariablesAsync(
        [Description("Stack frame index (0 = top)")]
        int? frameIndex = null,
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_variables", BuildArgs(
            ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.set_variable", Title = "Set Variable Value",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Set the value of a variable in the current debug context.")]
    public Task<string> SetVariableAsync(
        [Description("Variable name")] string variableName,
        [Description("New value expression")] string value,
        [Description("Stack frame index (0 = top)")]
        int? frameIndex = null,
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("set_variable", BuildArgs(
            ("variableName", variableName), ("value", value),
            ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);

    // -- Stack ------------------------------------------------------------

    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.get_stack_trace", Title = "Get Stack Trace",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get the current stack trace.")]
    public Task<string> GetStackTraceAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_stack_trace", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.list_threads", Title = "List Threads",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("List all threads in the debugged process.")]
    public Task<string> ListThreadsAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("list_threads", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.select_frame", Title = "Select Stack Frame",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Select a stack frame to inspect variables and evaluate expressions.")]
    public Task<string> SelectStackFrameAsync(
        [Description("Frame index to select")] int frameIndex,
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("select_stack_frame", BuildArgs(
            ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);

    // -- Source -----------------------------------------------------------

    [McpServerTool(Name = "qyl.debug.get_source", Title = "Get Source Context",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Get source code around the current execution point.")]
    public Task<string> GetSourceContextAsync(
        [Description("Debug session ID (omit for active)")]
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_source_context", BuildArgs(("sessionId", sessionId)), ct);

    // -- Proxy plumbing ---------------------------------------------------

    private async Task<string> CallRiderToolAsync(
        string riderToolName,
        Dictionary<string, object>? arguments,
        CancellationToken ct) =>
        await ProxyAsync(async () =>
        {
            var result = await proxy.CallToolAsync(riderToolName, arguments, ct).ConfigureAwait(false);
            return FormatResult(result);
        });

    private static async Task<string> ProxyAsync(Func<Task<string>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Rider"))
        {
            return $"Rider not connected: {ex.Message}";
        }
    }

    private static string FormatResult(CallToolResult result)
    {
        if (result.Content is not { Count: > 0 })
            return "No output from debugger.";

        var sb = new StringBuilder();
        foreach (var content in result.Content)
        {
            if (content is TextContentBlock text)
                sb.AppendLine(text.Text);
        }

        return sb.Length > 0 ? sb.ToString() : "No text output from debugger.";
    }

    private static Dictionary<string, object>? BuildArgs(params (string key, object? value)[] pairs)
    {
        Dictionary<string, object>? args = null;
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
            {
                args ??= [];
                args[key] = value;
            }
        }

        return args;
    }
}
