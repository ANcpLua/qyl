// Copyright (c) 2025-2026 ancplua

namespace Qyl.Instrumentation.Generators.Analyzers;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
///     QYL0135 — Warns when an <c>AIAgent</c> / <c>ChatClientAgent</c> is invoked from an inline
///     <c>new *Agent(...)</c> that lacks <c>.AsBuilder().UseOpenTelemetry(...).Build()</c>. Receivers
///     produced by factories or DI are trusted (the factory is the composition root).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentCompositionRootAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QYL0135";

    private static readonly string[] SAgentTypes =
    [
        "Microsoft.Agents.AI.AIAgent",
        "Microsoft.Agents.AI.ChatClientAgent",
        "Microsoft.Agents.AI.DelegatingAIAgent"
    ];

    private static readonly string[] STargetMethods =
        ["RunAsync", "RunStreamingAsync", "InvokeAsync", "CreateSessionAsync"];

    private static readonly DiagnosticDescriptor SRule = new(
        DiagnosticId,
        "Agent invoked without composition-root OpenTelemetry wrapping",
        "Agent '{0}' is invoked without composition-root OpenTelemetry wrapping. " +
        "Wrap construction with `.AsBuilder().UseOpenTelemetry(\"qyl.agent\").Build()` " +
        "or resolve from a factory that applies the wrap.",
        "Qyl.Instrumentation",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var agents = SAgentTypes
                .Select(start.Compilation.GetTypeByMetadataName)
                .Where(symbol => symbol is not null)
                .Select(symbol => symbol!)
                .ToImmutableArray();

            if (agents.Length is 0)
                return;

            start.RegisterOperationAction(ctx => Analyze(ctx, agents), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, ImmutableArray<INamedTypeSymbol> agents)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!STargetMethods.Contains(invocation.TargetMethod.Name, StringComparer.Ordinal))
            return;

        if (invocation.Instance?.Type is not { } receiverType || !IsAgentType(receiverType, agents))
            return;

        if (!TryUnwrappedName(invocation.Instance, out var name))
            return;

        context.ReportDiagnostic(Diagnostic.Create(SRule, invocation.Syntax.GetLocation(), name));
    }

    private static bool IsAgentType(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> agents)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (agents.Any(agent => SymbolEqualityComparer.Default.Equals(t, agent)))
                return true;

        return false;
    }

    private static bool TryUnwrappedName(IOperation receiver, out string name)
    {
        name = receiver.Type?.Name ?? "agent";

        if (receiver is IObjectCreationOperation create)
        {
            name = create.Type?.Name ?? name;
            return true;
        }

        if (receiver is not ILocalReferenceOperation local)
            return false;

        name = local.Local.Name;

        if (local.Local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()
            is not VariableDeclaratorSyntax { Initializer.Value: { } initValue })
            return false;

        var semanticModel = receiver.SemanticModel;
        if (semanticModel?.GetOperation(initValue) is not { } initOp)
            return false;

        return IsRawConstruction(initOp);
    }

    private static bool IsRawConstruction(IOperation op)
    {
        while (op is IConversionOperation conv)
            op = conv.Operand;

        // Conservative design: only flag direct `new *Agent(...)` constructions. Any fluent
        // chain, factory call, or DI resolution is trusted — the composition root is assumed
        // to apply `.AsBuilder().UseOpenTelemetry(...).Build()`. Prefers false-negatives over
        // false-positives; fluent chains that forget the wrap are not caught here.
        return op is IObjectCreationOperation;
    }
}
