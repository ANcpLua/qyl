using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.Emitters;

/// <summary>
///     Emits assembly-level <c>[GeneratedCapabilityAttribute]</c> entries that declare
///     compile-time capabilities for OTel Resource attribute registration.
/// </summary>
internal static class CapabilityEmitter
{
    public static string Emit(CapabilityRegistration capabilities)
    {
        if (CapabilityManifest.IsEmpty(capabilities))
            return string.Empty;

        var sb = new StringBuilder();
        EmitterHelpers.AppendFileHeader(sb, suppressWarnings: true);

        foreach (var capability in CapabilityManifest.Enumerate(capabilities))
        {
            AppendAttributes(sb, capability.Kind, capability.Values);
        }

        return sb.ToString();
    }

    private static void AppendAttributes(StringBuilder sb, string kind, EquatableArray<string> values)
    {
        foreach (var value in values)
        {
            sb.AppendLine(
                $"[assembly: global::Qyl.Instrumentation.GeneratedCapabilityAttribute({Literal(kind)}, {Literal(value)})]");
        }
    }

    private static string Literal(string s) => SymbolDisplay.FormatLiteral(s, quote: true);
}
