// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     QYL0135 — Warns when an <c>AIAgent</c> is invoked from a construction that lacks
///     composition-root agent-layer telemetry. Detects both <c>new *Agent(...)</c> direct
///     construction and <c>xxx.AsAIAgent(...)</c> extension-call results assigned to a local,
///     when neither is wrapped by an <c>AIAgentBuilder</c> chain that calls one of:
///     <c>UseQylAgentTelemetry</c>, <c>UseOpenTelemetry</c>, or <c>UseLogging</c>.
///     Receivers produced by factories or DI are trusted (the factory is the composition root).
/// </summary>
/// <remarks>
///     <para>
///         Detection is fully symbol-based. Every method the analyzer cares about — the
///         invocation entry points (<c>RunAsync</c> et al.), <c>AIAgentBuilder.Build</c>, the
///         <c>AsAIAgent</c> extension family, and the three telemetry extensions — is resolved
///         once at compilation start via <see cref="Compilation.GetTypeByMetadataName" /> and
///         compared through <see cref="SymbolEqualityComparer" />. String-name matches are avoided
///         throughout so renames, namespace collisions, and unrelated methods with the same name
///         cannot false-fire or silently disable the diagnostic.
///     </para>
///     <para>
///         Method-name resolution is all-or-nothing — if any entry-point name fails to resolve
///         across every agent type in the compilation, the analyzer bails entirely rather than
///         degrade to partial coverage. Symbol-valued state is captured via closure locals
///         (not stored in a named container type) to comply with ANcpLua AL0119.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AgentCompositionRootAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "QYL0135";

    private const string AgentBuilderMetadataName = "Microsoft.Agents.AI.AIAgentBuilder";
    private const string ChatClientExtensionsMetadataName = "Microsoft.Agents.AI.ChatClientExtensions";

    private const string OpenTelemetryExtensionsMetadataName =
        "Microsoft.Agents.AI.OpenTelemetryAgentBuilderExtensions";

    private const string LoggingExtensionsMetadataName = "Microsoft.Agents.AI.LoggingAgentBuilderExtensions";

    private const string QylTelemetryExtensionsMetadataName =
        "Qyl.Instrumentation.Instrumentation.GenAi.GenAiInstrumentation";

    private static readonly string[] SAgentTypeMetadataNames =
    [
        "Microsoft.Agents.AI.AIAgent",
        "Microsoft.Agents.AI.ChatClientAgent",
        "Microsoft.Agents.AI.DelegatingAIAgent"
    ];

    private static readonly string[] STargetMethodNames =
        ["RunAsync", "RunStreamingAsync", "InvokeAsync", "CreateSessionAsync"];

    private static readonly DiagnosticDescriptor SRule = new(
        DiagnosticId,
        "Agent invoked without composition-root agent-layer telemetry wrapping",
        "Agent '{0}' is invoked without composition-root telemetry. " +
        "Wrap construction with `.AsBuilder().UseQylAgentTelemetry().Build()` " +
        "(or resolve from a factory that applies the wrap).",
        "Qyl.Instrumentation",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;

            var agents = SAgentTypeMetadataNames
                .Select(compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableArray();

            if (agents.Length is 0)
                return;

            var agentBuilder = compilation.GetTypeByMetadataName(AgentBuilderMetadataName);
            if (agentBuilder is null)
                return;

            // All-or-nothing method-name resolution: if any expected entry point fails to
            // resolve across every agent type, bail entirely. A partial green state from a
            // silently-shrunk set is worse than no analyzer.
            var targetMethodsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
            foreach (var name in STargetMethodNames)
            {
                var resolved = agents
                    .SelectMany(agent => agent.GetMembers(name))
                    .OfType<IMethodSymbol>()
                    .ToImmutableArray();

                if (resolved.IsEmpty)
                    return;

                targetMethodsBuilder.AddRange(resolved);
            }

            var targetMethods = targetMethodsBuilder.ToImmutable();

            var buildMethods = agentBuilder.GetMembers("Build")
                .OfType<IMethodSymbol>()
                .ToImmutableArray();

            var asAIAgentMethods = ResolveStaticMethods(compilation, ChatClientExtensionsMetadataName, "AsAIAgent");

            var telemetryMethods =
                ResolveStaticMethods(compilation, OpenTelemetryExtensionsMetadataName, "UseOpenTelemetry")
                    .AddRange(ResolveStaticMethods(compilation, LoggingExtensionsMetadataName, "UseLogging"))
                    .AddRange(ResolveStaticMethods(compilation, QylTelemetryExtensionsMetadataName,
                        "UseQylAgentTelemetry"));

            // Symbol-valued state is held in this closure's capture frame (compiler-generated,
            // not a qyl-authored named type) to satisfy AL0119. The analyzer runs once per
            // compilation, so the closure lifetime matches the analysis lifetime.
            start.RegisterOperationAction(
                ctx => Analyze(ctx, agents, targetMethods, buildMethods, asAIAgentMethods, telemetryMethods),
                OperationKind.Invocation);
        });
    }

    private static ImmutableArray<IMethodSymbol> ResolveStaticMethods(
        Compilation compilation, string containingTypeMetadataName, string methodName)
    {
        var type = compilation.GetTypeByMetadataName(containingTypeMetadataName);
        return type?.GetMembers(methodName).OfType<IMethodSymbol>().ToImmutableArray()
               ?? ImmutableArray<IMethodSymbol>.Empty;
    }

    private static void Analyze(
        OperationAnalysisContext operationContext,
        ImmutableArray<INamedTypeSymbol> agents,
        ImmutableArray<IMethodSymbol> targetMethods,
        ImmutableArray<IMethodSymbol> buildMethods,
        ImmutableArray<IMethodSymbol> asAIAgentMethods,
        ImmutableArray<IMethodSymbol> telemetryMethods)
    {
        var invocation = (IInvocationOperation)operationContext.Operation;

        if (!IsOneOf(invocation.TargetMethod, targetMethods))
            return;

        if (invocation.Instance?.Type is not { } receiverType || !IsAgentType(receiverType, agents))
            return;

        if (!TryUnwrappedName(invocation.Instance, buildMethods, asAIAgentMethods, telemetryMethods, out var name))
            return;

        operationContext.ReportDiagnostic(Diagnostic.Create(SRule, invocation.Syntax.GetLocation(), name));
    }

    private static bool IsAgentType(ITypeSymbol type, ImmutableArray<INamedTypeSymbol> agents)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            var current = t;
            if (agents.Any(agent => SymbolEqualityComparer.Default.Equals(current, agent)))
                return true;
        }

        return false;
    }

    private static bool TryUnwrappedName(
        IOperation receiver,
        ImmutableArray<IMethodSymbol> buildMethods,
        ImmutableArray<IMethodSymbol> asAIAgentMethods,
        ImmutableArray<IMethodSymbol> telemetryMethods,
        out string name)
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

        return IsBareAgentConstruction(initOp, buildMethods, asAIAgentMethods, telemetryMethods);
    }

    // Flags a construction as lacking agent-layer telemetry when:
    //   1) `new *Agent(...)` with no subsequent wrap — caller assigned the raw instance.
    //   2) `xxx.AsAIAgent(...)` invocation resolving (by symbol) to one of the MAF-published
    //      extensions, with no subsequent `.AsBuilder()...Build()` chain.
    //   3) `.AsBuilder().X().Y().Build()` chain where the terminal `Build` resolves to
    //      `AIAgentBuilder.Build` and none of the intermediate invocations resolves to a
    //      captured telemetry-extension method.
    // Factory and DI resolutions (method calls returning an AIAgent that are NOT one of the
    // recognised construction shapes) remain trusted.
    private static bool IsBareAgentConstruction(
        IOperation op,
        ImmutableArray<IMethodSymbol> buildMethods,
        ImmutableArray<IMethodSymbol> asAIAgentMethods,
        ImmutableArray<IMethodSymbol> telemetryMethods)
    {
        while (op is IConversionOperation conv)
            op = conv.Operand;

        return op switch
        {
            IObjectCreationOperation => true,
            IInvocationOperation invocation when IsOneOf(invocation.TargetMethod, asAIAgentMethods) => true,
            IInvocationOperation invocation when IsOneOf(invocation.TargetMethod, buildMethods)
                => !ChainHasAgentTelemetry(invocation.Instance, telemetryMethods),
            _ => false
        };
    }

    private static bool IsOneOf(IMethodSymbol method, ImmutableArray<IMethodSymbol> candidates)
    {
        if (candidates.IsDefaultOrEmpty) return false;

        var definition = method.OriginalDefinition;
        foreach (var candidate in candidates)
        {
            if (SymbolEqualityComparer.Default.Equals(definition, candidate.OriginalDefinition))
                return true;
        }

        return false;
    }

    // Walks an `AIAgentBuilder` fluent chain backwards from its terminal invocation, looking
    // for any symbol in the captured telemetry-method set. Returns true as soon as one matches.
    private static bool ChainHasAgentTelemetry(IOperation? node, ImmutableArray<IMethodSymbol> telemetryMethods)
    {
        for (var current = node; current is IInvocationOperation invocation; current = invocation.Instance)
        {
            if (IsOneOf(invocation.TargetMethod, telemetryMethods))
                return true;
        }

        return false;
    }
}
