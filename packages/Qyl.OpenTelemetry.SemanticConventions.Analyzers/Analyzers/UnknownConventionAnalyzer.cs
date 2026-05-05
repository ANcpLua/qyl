
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Qyl.OpenTelemetry.SemanticConventions.Analyzers.Model;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnknownConventionAnalyzer : DiagnosticAnalyzer
{
    public const string OtelUnknownId = "QYLSC003A";

    public const string QylUnregisteredId = "QYLSC003B";

    private static readonly DiagnosticDescriptor s_otelUnknown = new(
        OtelUnknownId,
        "Unknown OTel-namespaced attribute",
        "Unknown OTel attribute '{0}', did you mean '{1}'",
        "QylSemanticConventions",
        DiagnosticSeverity.Warning,
        true,
        "The attribute ID starts with a known OTel namespace prefix but is not in the registry. This is likely a typo or a renamed/deprecated attribute.");

    private static readonly DiagnosticDescriptor s_qylUnregistered = new(
        QylUnregisteredId,
        "Unregistered qyl attribute",
        "Unregistered qyl attribute '{0}'. Add it to eng/semconv/qyl/model/ before shipping.",
        "QylSemanticConventions",
        DiagnosticSeverity.Warning,
        true,
        "Attributes under 'qyl.' must be registered in the qyl semantic conventions model.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(s_otelUnknown, s_qylUnregistered);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
    {
        try
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;
            var literal = TagMethodMatcher.TryGetStringKeyArgument(invocation);
            if (literal is null)
                return;

            var value = literal.Token.ValueText;
            var dep = DeprecationIndex.Instance;
            var reg = RegistryIndex.Instance;

            if (dep.DeprecatedIds.Contains(value) || reg.IsValid(value))
                return;

            if (value.StartsWith("qyl.", StringComparison.OrdinalIgnoreCase))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(s_qylUnregistered, literal.GetLocation(), value));
                return;
            }

            if (reg.HasOtelPrefix(value))
            {
                var suggestion = FindClosestMatch(value, reg.ValidIds);
                ctx.ReportDiagnostic(Diagnostic.Create(
                    s_otelUnknown, literal.GetLocation(), value, suggestion ?? "(no close match)"));
            }
        }
        catch
        {
        }
    }

    private static string? FindClosestMatch(string candidate, ImmutableHashSet<string> validIds)
    {
        string? best = null;
        var bestDist = int.MaxValue;
        const int maxDist = 3;

        var dot = candidate.IndexOf('.');
        var prefix = dot > 0 ? candidate.Substring(0, dot) : candidate;

        foreach (var id in validIds)
        {
            if (!id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var dist = Levenshtein(candidate, id);
            if (dist < bestDist && dist <= maxDist)
            {
                bestDist = dist;
                best = id;
            }
        }

        return best;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        for (var j = 1; j <= b.Length; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            d[i, j] = Math.Min(
                Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                d[i - 1, j - 1] + cost);
        }

        return d[a.Length, b.Length];
    }
}
