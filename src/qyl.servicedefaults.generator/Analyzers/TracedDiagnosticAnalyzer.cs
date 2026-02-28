using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using ANcpLua.Roslyn.Utilities.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class TracedDiagnosticAnalyzer : DiagnosticAnalyzerBase
{
    private const string TracedFull = "Qyl.ServiceDefaults.Instrumentation.TracedAttribute";
    private const string TracedTagFull = "Qyl.ServiceDefaults.Instrumentation.TracedTagAttribute";
    private const string NoTraceFull = "Qyl.ServiceDefaults.Instrumentation.NoTraceAttribute";

    private static readonly DiagnosticDescriptor QSD001 = new(
        "QSD001", "Orphaned [TracedTag]",
        "[TracedTag] on '{0}' has no effect — neither the method nor its declaring type has [Traced].",
        "Qyl.Tracing", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor QSD002 = new(
        "QSD002", "Redundant [NoTrace]",
        "[NoTrace] on '{0}' has no effect — the declaring type has no class-level [Traced].",
        "Qyl.Tracing", DiagnosticSeverity.Info, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor QSD003 = new(
        "QSD003", "Non-interceptable [Traced]",
        "[Traced] on '{0}' will be ignored — abstract, extern, and partial methods cannot be intercepted.",
        "Qyl.Tracing", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor QSD005 = new(
        "QSD005", "[TracedTag] on out/ref parameter",
        "[TracedTag] on '{0}' is not supported for out or ref parameters.",
        "Qyl.Tracing", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [QSD001, QSD002, QSD003, QSD005];

    protected override void InitializeCore(AnalysisContext context)
    {
        context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
    }

    private void AnalyzeMethod(SymbolAnalysisContext ctx)
    {
        if (ctx.Symbol is not IMethodSymbol method) return;

        var tracedType = ctx.Compilation.GetTypeByMetadataName(TracedFull);
        var tracedTagType = ctx.Compilation.GetTypeByMetadataName(TracedTagFull);
        var noTraceType = ctx.Compilation.GetTypeByMetadataName(NoTraceFull);

        var methodHasTraced = tracedType is not null && method.HasAttribute(tracedType);
        var classHasTraced = tracedType is not null && HasTracedOnType(method.ContainingType, tracedType);

        // QSD003: [Traced] on non-interceptable method
        if (methodHasTraced && (method.IsAbstract || method.IsExtern || method.IsPartialDefinition))
            ctx.ReportDiagnostic(Diagnostic.Create(QSD003, method.Locations[0], method.Name));

        // QSD002: [NoTrace] without class-level [Traced]
        if (noTraceType is not null && method.HasAttribute(noTraceType) && !classHasTraced)
            ctx.ReportDiagnostic(Diagnostic.Create(QSD002, method.Locations[0], method.Name));

        // Parameter-level checks
        if (tracedTagType is null) return;
        foreach (var param in method.Parameters)
        {
            if (!param.HasAttribute(tracedTagType)) continue;

            // QSD001: [TracedTag] with no [Traced] in scope
            if (!methodHasTraced && !classHasTraced)
                ctx.ReportDiagnostic(Diagnostic.Create(QSD001, param.Locations[0], param.Name));

            // QSD005: out/ref parameter
            if (param.RefKind is RefKind.Out or RefKind.Ref)
                ctx.ReportDiagnostic(Diagnostic.Create(QSD005, param.Locations[0], param.Name));
        }
    }

    private static bool HasTracedOnType(INamedTypeSymbol? type, INamedTypeSymbol tracedType)
    {
        while (type is not null)
        {
            if (type.HasAttribute(tracedType)) return true;
            type = type.BaseType;
        }
        return false;
    }
}
