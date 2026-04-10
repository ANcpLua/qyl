using qyl.mcp.Apps.ErrorExplorer;
using qyl.mcp.Apps.QueryStudio;
using qyl.mcp.Apps.TraceExplorer;
using qyl.mcp.Tools;
using qyl.mcp.Tools.Analysis;
using qyl.mcp.Tools.Auth;
using qyl.mcp.Tools.Debug;
using qyl.mcp.Tools.Discovery;
using qyl.mcp.Tools.Errors;
using qyl.mcp.Tools.Intelligence;
using qyl.mcp.Tools.Logs;
using qyl.mcp.Tools.Management;
using qyl.mcp.Tools.Metrics;
using qyl.mcp.Tools.Sessions;
using qyl.mcp.Tools.Traces;
using qyl.mcp.Tools.Triage;

namespace qyl.mcp.Skills;

internal static class QylSkillCatalog
{
    private static readonly Dictionary<string, QylSkillKind> SkillMap =
        new Dictionary<string, QylSkillKind>(StringComparer.Ordinal)
        {
            [Normalize(typeof(TelemetryTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(ReplayTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(StructuredLogTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(GenAiTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(ErrorTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(ServiceTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(SpanQueryTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(IntelligenceTools))] = QylSkillKind.Inspect,
            [Normalize(typeof(SearchTracesTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetTraceDetailsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetSpanTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(SearchLogsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetLogDetailsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(ListMetricsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(QueryMetricsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(SearchSessionsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetSessionTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(ListProjectsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(ListServicesTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetServiceMapTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(AnnotateTraceTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(MarkTraceReviewedTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(AnnotateSessionTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(UpdateSessionStatusTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(WhoamiTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetBreadcrumbsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetAttachmentsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetTagDistributionTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetProfileTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(GetReleaseHealthTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(SetErrorPriorityTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(MergeErrorsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(LinkErrorsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(SnoozeErrorTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(CreateTeamTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(ListTeamsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(CreateDsnTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(ListDsnsTool))] = QylSkillKind.Inspect,
            [Normalize(typeof(StorageHealthTools))] = QylSkillKind.Health,
            [Normalize(typeof(AnalyticsTools))] = QylSkillKind.Analytics,
            [Normalize(typeof(UseQylTools))] = QylSkillKind.Agent,
            [Normalize(typeof(RcaTools))] = QylSkillKind.Agent,
            [Normalize(typeof(SummaryTools))] = QylSkillKind.Agent,
            [Normalize(typeof(AnalyzeTraceTool))] = QylSkillKind.Agent,
            [Normalize(typeof(AnalyzeSessionTool))] = QylSkillKind.Agent,
            [Normalize(typeof(SuggestFixTool))] = QylSkillKind.Agent,
            [Normalize(typeof(CreateProjectTool))] = QylSkillKind.Build,
            [Normalize(typeof(UpdateProjectTool))] = QylSkillKind.Build,
            [Normalize(typeof(ConfigureRetentionTool))] = QylSkillKind.Build,
            [Normalize(typeof(CreateApiKeyTool))] = QylSkillKind.Build,
            [Normalize(typeof(AnomalyTools))] = QylSkillKind.Anomaly,
            [Normalize(typeof(TriageTools))] = QylSkillKind.Loom,
            [Normalize(typeof(ExportForAgentTools))] = QylSkillKind.Loom,
            [Normalize(typeof(FixTools))] = QylSkillKind.Loom,
            [Normalize(typeof(AutofixMcpTools))] = QylSkillKind.Loom,
            [Normalize(typeof(RegressionTools))] = QylSkillKind.Loom,
            [Normalize(typeof(GitHubMcpTools))] = QylSkillKind.Loom,
            [Normalize(typeof(AssistedQueryTools))] = QylSkillKind.Loom,
            [Normalize(typeof(TestGenerationTools))] = QylSkillKind.Loom,
            [Normalize(typeof(TraceExplorerTools))] = QylSkillKind.Apps,
            [Normalize(typeof(ErrorExplorerTools))] = QylSkillKind.Apps,
            [Normalize(typeof(QueryStudioTools))] = QylSkillKind.Apps,
            [Normalize(typeof(DebugTools))] = QylSkillKind.Debug,
        };

    public static bool TryGetSkill(string declaringType, out QylSkillKind skill) =>
        SkillMap.TryGetValue(Normalize(declaringType), out skill);

    public static string GetSkillLabel(string declaringType) =>
        TryGetSkill(declaringType, out var skill)
            ? skill.ToString().ToLowerInvariant()
            : "core";

    public static bool IsEnabled(string declaringType, SkillConfiguration skills) =>
        !TryGetSkill(declaringType, out var skill) || skills.IsEnabled(skill);

    public static IReadOnlyList<string> GetEnabledSkillLabels(SkillConfiguration skills)
    {
        List<string> labels = ["core"];
        foreach (var skill in Enum.GetValues<QylSkillKind>())
        {
            if (skills.IsEnabled(skill))
                labels.Add(skill.ToString().ToLowerInvariant());
        }

        return labels;
    }

    private static string Normalize(Type type) => Normalize(type.FullName ?? type.Name);

    private static string Normalize(string typeName) =>
        typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName[8..]
            : typeName;
}
