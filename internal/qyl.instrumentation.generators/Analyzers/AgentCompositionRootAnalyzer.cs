
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Qyl.Instrumentation.Generators.Analyzers;

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

    private static readonly string[] s_agentTypeMetadataNames =
    [
        "Microsoft.Agents.AI.AIAgent",
        "Microsoft.Agents.AI.ChatClientAgent",
        "Microsoft.Agents.AI.DelegatingAIAgent"
    ];

    private static readonly string[] s_targetMethodNames =
        ["RunAsync", "RunStreamingAsync", "InvokeAsync", "CreateSessionAsync"];

    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "Agent invoked without composition-root agent-layer telemetry wrapping",
        "Agent '{0}' is invoked without composition-root telemetry. " +
        "Wrap construction with `.AsBuilder().UseQylAgentTelemetry().Build()` " +
        "(or resolve from a factory that applies the wrap).",
        "Qyl.Instrumentation",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [s_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var compilation = start.Compilation;

            var agents = s_agentTypeMetadataNames
                .Select(compilation.GetTypeByMetadataName)
                .OfType<INamedTypeSymbol>()
                .ToImmutableArray();

            if (agents.Length is 0)
                return;

            var agentBuilder = compilation.GetTypeByMetadataName(AgentBuilderMetadataName);
            if (agentBuilder is null)
                return;

            var targetMethodsBuilder = ImmutableArray.CreateBuilder<IMethodSymbol>();
            foreach (var name in s_targetMethodNames)
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

        operationContext.ReportDiagnostic(Diagnostic.Create(s_rule, invocation.Syntax.GetLocation(), name));
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
