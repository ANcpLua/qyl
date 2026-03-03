using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
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
        }

        // Build: build failures and analysis
        if (skills.IsEnabled(QylSkillKind.Build))
        {
            mcpBuilder.WithTools<BuildTools>(jsonOptions);
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

        return mcpBuilder;
    }
}
