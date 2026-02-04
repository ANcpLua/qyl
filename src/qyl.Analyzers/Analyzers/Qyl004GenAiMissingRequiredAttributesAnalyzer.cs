using qyl.Analyzers.Core;

namespace qyl.Analyzers.Analyzers;

/// <summary>
///     QYL004: Detects GenAI spans that are missing required semantic convention attributes.
/// </summary>
/// <remarks>
///     <para>
///         GenAI spans require these attributes for proper observability:
///         <list type="bullet">
///             <item>gen_ai.provider.name - The GenAI provider (e.g., "openai")</item>
///             <item>gen_ai.request.model - The model name</item>
///             <item>gen_ai.operation.name - The operation type</item>
///         </list>
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class Qyl004GenAiMissingRequiredAttributesAnalyzer : QylAnalyzer
{
    private static readonly string[] RequiredGenAiAttributes =
    [
        "gen_ai.provider.name",
        "gen_ai.request.model",
        "gen_ai.operation.name"
    ];

    private static readonly LocalizableResourceString Title = new(
        nameof(Resources.QYL004AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString MessageFormat = new(
        nameof(Resources.QYL004AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));

    private static readonly LocalizableResourceString Description = new(
        nameof(Resources.QYL004AnalyzerDescription), Resources.ResourceManager, typeof(Resources));

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.GenAiMissingRequiredAttributes,
        Title, MessageFormat, DiagnosticCategories.GenAI,
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

        // Check if the activity name contains "gen_ai" or "genai" (case insensitive)
        var activityName = GetActivityName(invocation);
        if (activityName is null || !IsGenAiActivity(activityName))
        {
            return;
        }

        // Find the method body and collect all SetTag calls
        var containingMethod = invocation.SemanticModel?.GetEnclosingSymbol(
            invocation.Syntax.SpanStart, context.CancellationToken) as IMethodSymbol;

        if (containingMethod is null)
        {
            return;
        }

        // Collect set tags in the current method context
        var setTags = CollectSetTagCalls(invocation);

        // Check for missing required attributes
        foreach (var requiredAttribute in RequiredGenAiAttributes)
        {
            if (!setTags.Contains(requiredAttribute, StringComparer.OrdinalIgnoreCase))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    invocation.Syntax.GetLocation(),
                    activityName,
                    requiredAttribute));
            }
        }
    }

    private static string? GetActivityName(IInvocationOperation invocation)
    {
        // First argument is typically the activity name
        if (invocation.Arguments.Length > 0 &&
            invocation.Arguments[0].Value.ConstantValue is { HasValue: true, Value: string name })
        {
            return name;
        }

        return null;
    }

    private static bool IsGenAiActivity(string activityName) =>
        activityName.Contains("gen_ai", StringComparison.OrdinalIgnoreCase) ||
        activityName.Contains("genai", StringComparison.OrdinalIgnoreCase) ||
        activityName.Contains("chat", StringComparison.OrdinalIgnoreCase) ||
        activityName.Contains("completion", StringComparison.OrdinalIgnoreCase) ||
        activityName.Contains("embedding", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> CollectSetTagCalls(IInvocationOperation startActivity)
    {
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Walk sibling operations looking for SetTag calls
        var parent = startActivity.Parent;
        if (parent is null)
        {
            return tags;
        }

        // Simple heuristic: look for SetTag calls in the same block
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
