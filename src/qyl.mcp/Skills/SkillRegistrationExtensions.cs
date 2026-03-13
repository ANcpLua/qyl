using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Apps.QueryStudio;
using qyl.mcp.Apps.TraceExplorer;
using qyl.mcp.Tools;

namespace qyl.mcp.Skills;

/// <summary>
///     Extension methods for conditionally registering MCP tools based on enabled skills.
/// </summary>
internal static class SkillRegistrationExtensions
{
    /// <summary>
    ///     Registers all MCP tool classes, filtered by the <see cref="SkillConfiguration"/>.
    ///     When <c>QYL_SKILLS=all</c> (default), all tools are registered.
    ///     When narrowed (e.g., <c>QYL_SKILLS=inspect,health</c>), only matching tool classes load.
    /// </summary>
    public static IMcpServerBuilder WithSkillTools(
        this IMcpServerBuilder mcpBuilder,
        SkillConfiguration skills,
        JsonSerializerOptions jsonOptions)
    {
        // Inspect: telemetry, replay, logs, genai, errors, console, services, span queries
        if (skills.IsEnabled(QylSkillKind.Inspect))
        {
            mcpBuilder
                .WithTools<TelemetryTools>(jsonOptions)
                .WithTools<ReplayTools>(jsonOptions)
                .WithTools<ConsoleTools>(jsonOptions)
                .WithTools<StructuredLogTools>(jsonOptions)
                .WithTools<GenAiTools>(jsonOptions)
                .WithTools<ErrorTools>(jsonOptions)
                .WithTools<ServiceTools>(jsonOptions)
                .WithTools<SpanQueryTools>(jsonOptions);

            // Directory-facing inspect tools
            mcpBuilder
                .WithTools<Tools.Traces.SearchTracesTool>()
                .WithTools<Tools.Traces.GetTraceDetailsTool>()
                .WithTools<Tools.Traces.GetSpanTool>()
                .WithTools<Tools.Logs.SearchLogsTool>()
                .WithTools<Tools.Logs.GetLogDetailsTool>()
                .WithTools<Tools.Metrics.ListMetricsTool>()
                .WithTools<Tools.Metrics.QueryMetricsTool>()
                .WithTools<Tools.Sessions.SearchSessionsTool>()
                .WithTools<Tools.Sessions.GetSessionTool>()
                .WithTools<Tools.Discovery.ListProjectsTool>()
                .WithTools<Tools.Discovery.ListServicesTool>()
                .WithTools<Tools.Discovery.GetServiceMapTool>()
                // Triage (inspect-write)
                .WithTools<Tools.Triage.AnnotateTraceTool>()
                .WithTools<Tools.Triage.MarkTraceReviewedTool>()
                .WithTools<Tools.Sessions.AnnotateSessionTool>()
                .WithTools<Tools.Sessions.UpdateSessionStatusTool>();
        }

        // Health: storage stats, health check, system context
        if (skills.IsEnabled(QylSkillKind.Health))
        {
            mcpBuilder.WithTools<StorageHealthTools>(jsonOptions);
        }

        // Analytics: conversations, coverage gaps, satisfaction, user journeys
        if (skills.IsEnabled(QylSkillKind.Analytics))
        {
            mcpBuilder.WithTools<AnalyticsTools>(jsonOptions);
        }

        // Agent: investigate, use_qyl, RCA, summaries (LLM-powered)
        if (skills.IsEnabled(QylSkillKind.Agent))
        {
            mcpBuilder
                .WithTools<InvestigateTools>(jsonOptions)
                .WithTools<UseQylTools>(jsonOptions)
                .WithTools<RcaTools>(jsonOptions)
                .WithTools<SummaryTools>(jsonOptions);

            // Directory-facing analysis tools
            mcpBuilder
                .WithTools<Tools.Analysis.AnalyzeTraceTool>()
                .WithTools<Tools.Analysis.AnalyzeSessionTool>()
                .WithTools<Tools.Analysis.SuggestFixTool>();
        }

        // Build: build failures and analysis
        if (skills.IsEnabled(QylSkillKind.Build))
        {
            mcpBuilder.WithTools<BuildTools>(jsonOptions);

            // Directory-facing management tools
            mcpBuilder
                .WithTools<Tools.Management.CreateProjectTool>()
                .WithTools<Tools.Management.UpdateProjectTool>()
                .WithTools<Tools.Management.ConfigureRetentionTool>()
                .WithTools<Tools.Management.CreateApiKeyTool>();
        }

        // Anomaly: z-score detection, baselines, period comparison
        if (skills.IsEnabled(QylSkillKind.Anomaly))
        {
            mcpBuilder.WithTools<AnomalyTools>(jsonOptions);
        }

        // Copilot: GitHub Copilot chat, workflows, status
        if (skills.IsEnabled(QylSkillKind.Copilot))
        {
            mcpBuilder.WithTools<CopilotTools>(jsonOptions);
        }

        // ClaudeCode: Claude Code session telemetry
        if (skills.IsEnabled(QylSkillKind.ClaudeCode))
        {
            mcpBuilder.WithTools<ClaudeCodeTools>(jsonOptions);
        }

        // Loom: AI debugging pipeline — triage, autofix, code review, agent handoff
        if (skills.IsEnabled(QylSkillKind.Loom))
        {
            mcpBuilder
                .WithTools<TriageTools>(jsonOptions)
                .WithTools<ExportForAgentTools>(jsonOptions)
                .WithTools<FixTools>(jsonOptions)
                .WithTools<AutofixMcpTools>(jsonOptions)
                .WithTools<RegressionTools>(jsonOptions)
                .WithTools<GitHubMcpTools>(jsonOptions)
                .WithTools<AgentHandoffTools>(jsonOptions)
                .WithTools<AssistedQueryTools>(jsonOptions)
                .WithTools<TestGenerationTools>(jsonOptions);
        }

        // Apps: interactive ext-app UIs (trace viewer, error explorer, query studio)
        if (skills.IsEnabled(QylSkillKind.Apps))
        {
            mcpBuilder
                .WithTraceExplorer(jsonOptions)
                .WithErrorExplorer(jsonOptions)
                .WithQueryStudio(jsonOptions);
        }

        return mcpBuilder;
    }
}
