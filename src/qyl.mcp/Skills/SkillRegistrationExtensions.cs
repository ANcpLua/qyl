using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Apps.QueryStudio;
using qyl.mcp.Apps.TraceExplorer;
using qyl.mcp.Tools;
using qyl.mcp.Tools.Analysis;
using qyl.mcp.Tools.Discovery;
using qyl.mcp.Tools.Logs;
using qyl.mcp.Tools.Management;
using qyl.mcp.Tools.Metrics;
using qyl.mcp.Tools.Sessions;
using qyl.mcp.Tools.Traces;
using qyl.mcp.Tools.Triage;

namespace qyl.mcp.Skills;

/// <summary>
///     Extension methods for conditionally registering MCP tools based on enabled skills.
/// </summary>
internal static class SkillRegistrationExtensions
{
    /// <summary>
    ///     Registers all MCP tool classes, filtered by the <see cref="SkillConfiguration" />.
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
                .WithTools<SearchTracesTool>()
                .WithTools<GetTraceDetailsTool>()
                .WithTools<GetSpanTool>()
                .WithTools<SearchLogsTool>()
                .WithTools<GetLogDetailsTool>()
                .WithTools<ListMetricsTool>()
                .WithTools<QueryMetricsTool>()
                .WithTools<SearchSessionsTool>()
                .WithTools<GetSessionTool>()
                .WithTools<ListProjectsTool>()
                .WithTools<ListServicesTool>()
                .WithTools<GetServiceMapTool>()
                // Triage (inspect-write)
                .WithTools<AnnotateTraceTool>()
                .WithTools<MarkTraceReviewedTool>()
                .WithTools<AnnotateSessionTool>()
                .WithTools<UpdateSessionStatusTool>();
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

        // Agent: use_qyl, RCA, summaries (LLM-powered)
        if (skills.IsEnabled(QylSkillKind.Agent))
        {
            mcpBuilder
                .WithTools<UseQylTools>(jsonOptions)
                .WithTools<RcaTools>(jsonOptions)
                .WithTools<SummaryTools>(jsonOptions);

            // Directory-facing analysis tools
            mcpBuilder
                .WithTools<AnalyzeTraceTool>()
                .WithTools<AnalyzeSessionTool>()
                .WithTools<SuggestFixTool>();
        }

        // Build: build failures and analysis
        if (skills.IsEnabled(QylSkillKind.Build))
        {
            mcpBuilder.WithTools<BuildTools>(jsonOptions);

            // Directory-facing management tools
            mcpBuilder
                .WithTools<CreateProjectTool>()
                .WithTools<UpdateProjectTool>()
                .WithTools<ConfigureRetentionTool>()
                .WithTools<CreateApiKeyTool>();
        }

        // Anomaly: z-score detection, baselines, period comparison
        if (skills.IsEnabled(QylSkillKind.Anomaly))
        {
            mcpBuilder.WithTools<AnomalyTools>(jsonOptions);
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
