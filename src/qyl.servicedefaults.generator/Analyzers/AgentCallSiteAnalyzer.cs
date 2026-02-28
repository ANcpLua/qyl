using ANcpLua.Roslyn.Utilities;
using ANcpLua.Roslyn.Utilities.Matching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find Microsoft.Agents.AI agent invocations and [AgentTraced] methods.
/// </summary>
/// <remarks>
///     Detects:
///     - <c>AIAgent.InvokeAsync()</c>, <c>ChatClientAgent.InvokeAsync()</c>
///     - <c>DelegatingAIAgent</c> pipeline methods
///     - <c>AIAgentBuilder</c> registration calls
///     - Methods decorated with <c>[AgentTraced]</c>
/// </remarks>
internal static class AgentCallSiteAnalyzer
{
    private const string AgentTracedAttributeFullName = "Qyl.ServiceDefaults.Instrumentation.AgentTracedAttribute";

    /// <summary>
    ///     Declarative agent method patterns.
    /// </summary>
    private static readonly (InvocationMatcher Matcher, AgentCallKind Kind)[] Matchers = BuildMatchers();

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be an agent invocation?
    /// </summary>
    public static bool CouldBeAgentInvocation(SyntaxNode node, CancellationToken ct) =>
        AnalyzerHelpers.CouldBeInvocation(node, ct);

    /// <summary>
    ///     Extracts an agent call site from a syntax context if it matches agent SDK patterns
    ///     or has [AgentTraced] attribute.
    /// </summary>
    public static AgentCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (AnalyzerHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!AnalyzerHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        // Skip if already intercepted by another generator
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        // Try SDK pattern match first
        if (TryMatchAgentMethod(invocation, out var kind))
            return BuildCallSite(context, invocation, kind, cancellationToken);

        // Then try [AgentTraced] attribute
        return TryGetAgentTracedAttribute(invocation.TargetMethod, context.SemanticModel.Compilation, out var agentName) ? BuildCallSite(context, invocation, AgentCallKind.AgentTracedMethod, cancellationToken, agentName) : null;
    }

    private static AgentCallSite? BuildCallSite(
        GeneratorSyntaxContext context,
        IInvocationOperation invocation,
        AgentCallKind kind,
        CancellationToken cancellationToken,
        string? agentName = null)
    {
        if (context.SemanticModel.GetInterceptableLocation((InvocationExpressionSyntax)context.Node, cancellationToken) is not { } interceptLocation)
            return null;

        var method = invocation.TargetMethod;
        var isAsync = AnalyzerHelpers.IsAsyncReturnType(method);

        // For SDK calls, try to extract agent name from the type
        agentName ??= TryExtractAgentName(invocation);

        return new AgentCallSite(
            AnalyzerHelpers.FormatSortKey(context.Node),
            agentName,
            kind,
            method.ContainingType.ToDisplayString(),
            method.Name,
            isAsync,
            method.ReturnType.ToDisplayString(),
            method.Parameters.Select(static p => p.Type.ToDisplayString()).ToArray().ToEquatableArray(),
            method.Parameters.Select(static p => p.Name).ToArray().ToEquatableArray(),
            interceptLocation);
    }

    private static bool TryMatchAgentMethod(
        IInvocationOperation invocation,
        out AgentCallKind kind)
    {
        kind = default;

        foreach (var (matcher, k) in Matchers)
        {
            if (!matcher.Matches(invocation))
                continue;

            kind = k;
            return true;
        }

        return false;
    }

    private static bool TryGetAgentTracedAttribute(
        ISymbol method,
        Compilation compilation,
        [NotNullWhen(true)] out string? agentName)
    {
        agentName = null;

        if (!method.HasAttribute(AgentTracedAttributeFullName))
            return false;

        if (!TryFindAttributeData(method, compilation, out var attribute))
            return false;

        agentName = ExtractAgentName(attribute, fallback: method.Name);
        return true;
    }

    private static bool TryFindAttributeData(
        ISymbol method,
        Compilation compilation,
        [NotNullWhen(true)] out AttributeData? result)
    {
        result = null;

        if (compilation.GetTypeByMetadataName(AgentTracedAttributeFullName) is not { } attributeType)
            return false;

        foreach (var attribute in method.GetAttributes())
        {
            if (!attribute.AttributeClass.IsEqualTo(attributeType))
                continue;

            result = attribute;
            return true;
        }

        return false;
    }

    private static string ExtractAgentName(AttributeData attribute, string fallback)
    {
        foreach (var namedArg in attribute.NamedArguments)
        {
            if (namedArg is { Key: "AgentName", Value.Value: string name })
                return name;
        }

        return fallback;
    }

    /// <summary>
    ///     Tries to extract an agent name from the invocation receiver type.
    /// </summary>
    private static string? TryExtractAgentName(IInvocationOperation invocation)
    {
        var typeName = invocation.TargetMethod.ContainingType?.Name;
        return typeName;
    }

    private static (InvocationMatcher, AgentCallKind)[] BuildMatchers()
    {
        var invokeAsyncMethods = new[] { "InvokeAsync" };
        var builderMethods = new[] { "AddAgent", "UseAgent", "ConfigureAgent" };

        var agentTypes = new[]
        {
            "Microsoft.Agents.AI.AIAgent", "Microsoft.Agents.AI.ChatClientAgent",
            "Microsoft.Agents.AI.DelegatingAIAgent"
        };

        var result = new List<(InvocationMatcher, AgentCallKind)>();

        // Agent InvokeAsync patterns
        foreach (var typePrefix in agentTypes)
        foreach (var methodName in invokeAsyncMethods)
        {
            var prefix = typePrefix;
            var matcher = Invoke.Method(methodName)
                .Where(i => i.TargetMethod.ContainingType?.ToDisplayString()
                    .StartsWithIgnoreCase(prefix) == true);
            result.Add((matcher, AgentCallKind.InvokeAsync));
        }

        // AIAgentBuilder registration patterns
        foreach (var methodName in builderMethods)
        {
            var matcher = Invoke.Method(methodName)
                .Where(i => i.TargetMethod.ContainingType?.ToDisplayString()
                    .StartsWithIgnoreCase("Microsoft.Agents.AI.AIAgentBuilder") == true);
            result.Add((matcher, AgentCallKind.BuilderRegistration));
        }

        return [.. result];
    }
}
