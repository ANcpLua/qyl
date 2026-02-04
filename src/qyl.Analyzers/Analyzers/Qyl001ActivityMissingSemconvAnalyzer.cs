using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL001: Detects Activity/Span creation without semantic convention attributes.
/// </summary>
/// <remarks>
///     <para>
///         OpenTelemetry Activities (Spans) should include semantic convention attributes
///         appropriate for their operation type to enable correlation, filtering, and analysis.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl001ActivityMissingSemconvAnalyzer : QylAnalyzer
{
    // Operation types and their expected semantic convention prefixes
    private static readonly Dictionary<string, string[]> OperationTypePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["http"] = ["http.", "url.", "server.", "client."],
        ["db"] = ["db."],
        ["rpc"] = ["rpc."],
        ["messaging"] = ["messaging."],
        ["faas"] = ["faas."],
        ["gen_ai"] = ["gen_ai."]
    };

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL001AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL001AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL001AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.ActivityMissingSemconv,
        Title, MessageFormat, DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverities.Suggestion, true, Description,
        HelpLinkBase);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <summary>Registers operation actions to analyze Activity.StartActivity calls.</summary>
    protected override void RegisterActions(AnalysisContext context) =>
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        // Look for ActivitySource.StartActivity calls
        if (invocation.TargetMethod.Name != "StartActivity")
        {
            return;
        }

        // Get the activity name
        var activityName = GetActivityName(invocation);
        if (activityName is null)
        {
            return;
        }

        // Determine the operation type from the activity name
        var operationType = InferOperationType(activityName);
        if (operationType is null)
        {
            return; // Can't determine operation type, skip analysis
        }

        // Collect SetTag calls in the same scope
        var setTags = CollectSetTagCalls(invocation);

        // Check if any semantic convention attributes for this operation type are present
        if (!OperationTypePrefixes.TryGetValue(operationType, out var expectedPrefixes))
        {
            return;
        }

        var hasRelevantTags = setTags.Any(tag =>
            expectedPrefixes.Any(prefix => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));

        if (!hasRelevantTags)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.Syntax.GetLocation(),
                activityName,
                operationType));
        }
    }

    private static string? GetActivityName(IInvocationOperation invocation)
    {
        if (invocation.Arguments.Length > 0 &&
            invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string name })
        {
            return name;
        }

        return null;
    }

    private static string? InferOperationType(string activityName)
    {
        foreach (var kvp in OperationTypePrefixes)
        {
            if (activityName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Key;
            }
        }

        // Additional heuristics
        if (activityName.Contains("request", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("response", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("get", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("post", StringComparison.OrdinalIgnoreCase))
        {
            return "http";
        }

        if (activityName.Contains("query", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("execute", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("select", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("insert", StringComparison.OrdinalIgnoreCase))
        {
            return "db";
        }

        if (activityName.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("completion", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("embedding", StringComparison.OrdinalIgnoreCase) ||
            activityName.Contains("llm", StringComparison.OrdinalIgnoreCase))
        {
            return "gen_ai";
        }

        return null;
    }

    private static HashSet<string> CollectSetTagCalls(IInvocationOperation startActivity)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Walk sibling operations looking for SetTag calls
        var parent = startActivity.Parent;
        if (parent is null)
        {
            return tags;
        }

        foreach (var child in parent.ChildOperations)
        {
            if (child is IInvocationOperation childInvocation &&
                childInvocation.TargetMethod.Name == "SetTag" &&
                childInvocation.Arguments.Length >= 1 &&
                childInvocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string tagName })
            {
                tags.Add(tagName);
            }
        }

        return tags;
    }
}
