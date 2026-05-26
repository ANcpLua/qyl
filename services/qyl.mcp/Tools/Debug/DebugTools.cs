using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace qyl.mcp.Tools.Debug;

[McpServerToolType]
[QylSkill(QylSkillKind.Debug)]
internal sealed partial class DebugTools(RiderMcpProxy proxy, JetBrainsDiscovery discovery)
{

    [QylCapability("debugger_control")]
    [McpServerTool(Name = "qyl.debug.status", Title = "Debug Connection Status",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial string GetStatus(
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
    public async partial Task<string> ListAvailableToolsAsync(CancellationToken ct = default) =>
        await ProxyAsync(async () =>
        {
            var tools = await proxy.ListToolsAsync(ct).ConfigureAwait(false);
            var sb = new StringBuilder();
            sb.AppendLine($"# Rider Debugger Tools ({tools.Count})");
            foreach (var tool in tools)
            {
                var description = tool.Description is { Length: > 0 } d ? d[..Math.Min(d.Length, 80)] : string.Empty;
                sb.AppendLine($"- **{tool.Name}**: {description}");
            }
            return sb.ToString();
        });


    [QylCapability("debugger_control")]
    [McpServerTool(Name = "qyl.debug.start_session", Title = "Start Debug Session",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    public partial Task<string> StartDebugSessionAsync(
        string configurationName,
        CancellationToken ct = default) =>
        CallRiderToolAsync("start_debug_session",
            new Dictionary<string, object?> { ["configurationName"] = configurationName }, ct);

    [McpServerTool(Name = "qyl.debug.stop_session", Title = "Stop Debug Session",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> StopDebugSessionAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("stop_debug_session", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.list_sessions", Title = "List Debug Sessions",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> ListDebugSessionsAsync(CancellationToken ct = default) =>
        CallRiderToolAsync("list_debug_sessions", null, ct);

    [McpServerTool(Name = "qyl.debug.session_status", Title = "Get Debug Session Status",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> GetDebugSessionStatusAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_debug_session_status", BuildArgs(("sessionId", sessionId)), ct);


    [QylCapability("debugger_control")]
    [McpServerTool(Name = "qyl.debug.list_run_configs", Title = "List Run Configurations",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> ListRunConfigurationsAsync(CancellationToken ct = default) =>
        CallRiderToolAsync("list_run_configurations", null, ct);


    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.set_breakpoint", Title = "Set Breakpoint",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> SetBreakpointAsync(
        string filePath,
        int lineNumber,
        string? condition = null,
        string? logExpression = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("set_breakpoint", BuildArgs(
            ("filePath", filePath), ("lineNumber", lineNumber),
            ("condition", condition), ("logExpression", logExpression)), ct);

    [McpServerTool(Name = "qyl.debug.remove_breakpoint", Title = "Remove Breakpoint",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> RemoveBreakpointAsync(
        string breakpointId,
        CancellationToken ct = default) =>
        CallRiderToolAsync("remove_breakpoint", new Dictionary<string, object?> { ["breakpointId"] = breakpointId },
            ct);

    [McpServerTool(Name = "qyl.debug.list_breakpoints", Title = "List Breakpoints",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> ListBreakpointsAsync(CancellationToken ct = default) =>
        CallRiderToolAsync("list_breakpoints", null, ct);


    [McpServerTool(Name = "qyl.debug.resume", Title = "Resume Execution",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    public partial Task<string> ResumeAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("resume", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.pause", Title = "Pause Execution",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> PauseAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("pause", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.step_over", Title = "Step Over",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    public partial Task<string> StepOverAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("step_over", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.step_into", Title = "Step Into",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    public partial Task<string> StepIntoAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("step_into", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.step_out", Title = "Step Out",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    public partial Task<string> StepOutAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("step_out", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.run_to_line", Title = "Run to Line",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    public partial Task<string> RunToLineAsync(
        string filePath,
        int lineNumber,
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("run_to_line", BuildArgs(
            ("filePath", filePath), ("lineNumber", lineNumber), ("sessionId", sessionId)), ct);


    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.evaluate", Title = "Evaluate Expression",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> EvaluateAsync(
        string expression,
        int? frameIndex = null,
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("evaluate", BuildArgs(
            ("expression", expression), ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);

    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.get_variables", Title = "Get Variables",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> GetVariablesAsync(
        int? frameIndex = null,
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_variables", BuildArgs(
            ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.set_variable", Title = "Set Variable Value",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> SetVariableAsync(
        string variableName,
        string value,
        int? frameIndex = null,
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("set_variable", BuildArgs(
            ("variableName", variableName), ("value", value),
            ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);


    [QylCapability("debugger_control", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.debug.get_stack_trace", Title = "Get Stack Trace",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> GetStackTraceAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_stack_trace", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.list_threads", Title = "List Threads",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> ListThreadsAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("list_threads", BuildArgs(("sessionId", sessionId)), ct);

    [McpServerTool(Name = "qyl.debug.select_frame", Title = "Select Stack Frame",
        ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> SelectStackFrameAsync(
        int frameIndex,
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("select_stack_frame", BuildArgs(
            ("frameIndex", frameIndex), ("sessionId", sessionId)), ct);


    [McpServerTool(Name = "qyl.debug.get_source", Title = "Get Source Context",
        ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    public partial Task<string> GetSourceContextAsync(
        string? sessionId = null,
        CancellationToken ct = default) =>
        CallRiderToolAsync("get_source_context", BuildArgs(("sessionId", sessionId)), ct);


    private async Task<string> CallRiderToolAsync(
        string riderToolName,
        Dictionary<string, object?>? arguments,
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

    private static Dictionary<string, object?>? BuildArgs(params ReadOnlySpan<(string key, object? value)> pairs)
    {
        Dictionary<string, object?>? args = null;
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
