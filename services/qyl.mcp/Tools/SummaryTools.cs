using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using qyl.mcp.Agents;

namespace qyl.mcp.Tools;

[McpServerToolType]
[QylSkill(QylSkillKind.Agent)]
internal sealed partial class SummaryTools(HttpClient client, IQylMcpAgentsBuilder agents)
{
    private readonly SummaryFacade summary = new(client, agents);

    [QylCapability("agentic_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.summarize_error", Title = "Summarize Error",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> SummarizeErrorAsync(
        string issueId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(() => summary.SummarizeErrorAsync(issueId, ct));

    [QylCapability("agentic_investigation", QylCapabilityRole.FollowUp)]
    [McpServerTool(Name = "qyl.summarize_trace", Title = "Summarize Trace",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> SummarizeTraceAsync(
        string traceId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(() => summary.SummarizeTraceAsync(traceId, ct));

    [McpServerTool(Name = "qyl.summarize_session", Title = "Summarize Session",
        ReadOnly = true, Destructive = false, Idempotent = false, OpenWorld = true,
        TaskSupport = ToolTaskSupport.Optional)]
    public partial Task<string> SummarizeSessionAsync(
        string sessionId,
        CancellationToken ct = default) =>
        CollectorHelper.ExecuteAsync(() => summary.SummarizeSessionAsync(sessionId, ct));
}
