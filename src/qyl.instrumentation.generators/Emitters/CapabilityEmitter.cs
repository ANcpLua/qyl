using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.Emitters;

/// <summary>
///     Emits assembly-level <c>[GeneratedCapabilityAttribute]</c> entries that declare
///     compile-time capabilities for OTel Resource attribute registration.
/// </summary>
internal static class CapabilityEmitter
{
    public static string Emit(
        ImmutableArray<GenAiCallSite> genAiCallSites,
        ImmutableArray<AgentCallSite> agentCallSites)
    {
        if (genAiCallSites.IsDefaultOrEmpty && agentCallSites.IsDefaultOrEmpty)
            return string.Empty;

        var sb = new StringBuilder();
        EmitterHelpers.AppendFileHeader(sb, suppressWarnings: true);

        // GenAI capabilities: providers, models, operations
        var providers = new HashSet<string>(StringComparer.Ordinal);
        var models = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var site in genAiCallSites)
        {
            if (!string.IsNullOrEmpty(site.Provider)) providers.Add(site.Provider);
            if (!string.IsNullOrEmpty(site.Model)) models.Add(site.Model!);
            if (!string.IsNullOrEmpty(site.Operation)) operations.Add(site.Operation);
        }

        AppendAttributes(sb, "gen_ai.provider", providers);
        AppendAttributes(sb, "gen_ai.model", models);
        AppendAttributes(sb, "gen_ai.operation", operations);

        // Agent capabilities: agent names
        var agentNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var site in agentCallSites)
        {
            if (!string.IsNullOrEmpty(site.AgentName)) agentNames.Add(site.AgentName!);
        }

        AppendAttributes(sb, "agent.name", agentNames);

        return sb.ToString();
    }

    private static void AppendAttributes(StringBuilder sb, string kind, HashSet<string> values)
    {
        foreach (var value in values)
        {
            sb.AppendLine(
                $"[assembly: global::Qyl.Instrumentation.GeneratedCapabilityAttribute({Literal(kind)}, {Literal(value)})]");
        }
    }

    private static string Literal(string s) => SymbolDisplay.FormatLiteral(s, quote: true);
}
