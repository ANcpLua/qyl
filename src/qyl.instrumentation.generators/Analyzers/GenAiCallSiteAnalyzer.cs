using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.Instrumentation.Generators.Models;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     Discovers GenAI SDK call sites for compile-time capability manifest emission.
/// </summary>
/// <remarks>
///     <para>
///         Patterns cover OTel Semantic Conventions v1.40 operations:
///         chat, embeddings, text_completion, image_generation, speech, transcription, rerank.
///         Uses the <see cref="Invoke" /> DSL from ANcpLua.Roslyn.Utilities for declarative matching.
///     </para>
///     <para>
///         Runtime instrumentation is handled by <c>InstrumentedChatClient</c> (DelegatingChatClient
///         wrapper with provider/model/token enrichment from ChatClientMetadata + ChatResponse).
///         This analyzer feeds only the <c>CapabilityEmitter</c> for compile-time topology discovery
///         ([GeneratedCapabilityAttribute] for providers, models, operations).
///     </para>
/// </remarks>
internal static class GenAiCallSiteAnalyzer
{
    private static readonly string[] ModelParameterNames = ["model", "modelId", "deploymentName"];

    /// <summary>
    ///     Declarative GenAI method patterns.
    ///     Each entry pairs an <see cref="InvocationMatcher" /> with the OTel operation metadata it represents.
    ///     Type matching uses symbol comparison (Phase 2) rather than string prefixes.
    /// </summary>
    private static readonly (string MethodName, InvocationMatcher Matcher, string TypeMetadataName, string Operation,
        bool IsAsync)[] Matchers =
            BuildMatchers();

    private static readonly HashSet<string> CandidateMethodNames =
    [
        ..Matchers.Select(static matcher => matcher.MethodName)
    ];

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a GenAI invocation?
    ///     Delegates to <see cref="AnalyzerHelpers.CouldBeInvocation" />.
    /// </summary>
    public static bool CouldBeGenAiInvocation(SyntaxNode node, CancellationToken _) =>
        AnalyzerHelpers.GetInvokedMethodName(node) is { } methodName &&
        CandidateMethodNames.Contains(methodName);

    /// <summary>
    ///     Extracts a GenAI call site from a syntax context if it matches known SDK patterns.
    ///     Returns null if not a GenAI call or if already intercepted.
    /// </summary>
    public static GenAiCallSite? ExtractCallSite(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (AnalyzerHelpers.IsGeneratedFile(context.Node.SyntaxTree.FilePath))
            return null;

        if (!AnalyzerHelpers.TryGetInvocationOperation(context, cancellationToken, out var invocation))
            return null;

        if (!TryMatchGenAiMethod(invocation, context.SemanticModel.Compilation, out var provider, out var operation,
                out var isAsync))
            return null;

        // Skip if already intercepted by another generator
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        if (context.SemanticModel.GetInterceptableLocation((InvocationExpressionSyntax)context.Node, cancellationToken)
            is not { } interceptLocation)
            return null;

        var method = invocation.TargetMethod;
        var model = TryExtractModelName(invocation);

        return new GenAiCallSite(
            AnalyzerHelpers.FormatSortKey(context.Node),
            provider,
            operation,
            model,
            method.ContainingType.ToDisplayString(),
            method.Name,
            isAsync,
            method.ReturnType.ToDisplayString(),
            method.Parameters.Select(static p => p.Type.ToDisplayString()).ToArray().ToEquatableArray(),
            interceptLocation);
    }

    private static bool TryMatchGenAiMethod(
        IInvocationOperation invocation,
        Compilation compilation,
        [NotNullWhen(true)] out string? provider,
        [NotNullWhen(true)] out string? operation,
        out bool isAsync)
    {
        provider = null;
        operation = null;
        isAsync = false;

        foreach (var (_, matcher, typeMetadataName, op, async) in Matchers)
        {
            // Phase 1: cheap method-name match via Invoke DSL
            if (!matcher.Matches(invocation))
                continue;

            // Phase 2: symbol-based type check
            var expectedType = compilation.GetTypeByMetadataName(typeMetadataName);
            if (expectedType is null)
                continue;

            var containingType = invocation.TargetMethod.ContainingType;
            if (containingType is null || !containingType.IsEqualTo(expectedType))
                continue;

            provider = ProviderDetector.GetGenAiProviderId(containingType.ToDisplayString()) ?? "unknown";
            operation = op;
            isAsync = async;
            return true;
        }

        return false;
    }

    private static string? TryExtractModelName(IInvocationOperation invocation)
    {
        foreach (var parameterName in ModelParameterNames)
        {
            if (invocation.TryGetStringArgument(parameterName, out var modelValue) &&
                !string.IsNullOrEmpty(modelValue))
                return modelValue;
        }

        return null;
    }

    /// <summary>
    ///     Builds all GenAI invocation matchers from the known SDK method patterns.
    /// </summary>
    /// <remarks>
    ///     Each SDK type+method combination gets its own <see cref="InvocationMatcher" /> with a
    ///     <c>.Where()</c> predicate that checks the containing type's display string prefix.
    ///     This preserves the original matching semantics (StartsWith on fully-qualified type name)
    ///     while expressing each pattern declaratively via the <see cref="Invoke" /> DSL.
    /// </remarks>
    private static (string MethodName, InvocationMatcher Matcher, string TypeMetadataName, string Operation, bool
        IsAsync)[] BuildMatchers()
    {
        // Agent framework methods shared across multiple types
        var agentMethods = new (string MethodName, string Operation, bool IsAsync)[]
        {
            ("RunAsync", "invoke_agent", true), ("RunStreamingAsync", "invoke_agent", true)
        };

        // (type metadata name, methods) — type matching is done via symbol comparison in TryMatchGenAiMethod
        var patterns = new (string TypeMetadataName, (string MethodName, string Operation, bool IsAsync)[] Methods)[]
        {
            // Microsoft.Extensions.AI abstractions
            (
                "Microsoft.Extensions.AI.IChatClient",
                [("GetResponseAsync", "chat", true), ("GetStreamingResponseAsync", "chat", true)]
            ),

            // OpenAI SDK v2.x
            ("OpenAI.Chat.ChatClient", [("CompleteChatAsync", "chat", true), ("CompleteChat", "chat", false)]), (
                "OpenAI.Embeddings.EmbeddingClient",
                [("GenerateEmbeddingsAsync", "embeddings", true), ("GenerateEmbeddings", "embeddings", false)]),
            ("OpenAI.Images.ImageClient",
                [("GenerateImagesAsync", "image_generation", true), ("GenerateImages", "image_generation", false)]),
            ("OpenAI.Audio.AudioClient",
            [
                ("GenerateSpeechAsync", "speech", true), ("GenerateSpeech", "speech", false),
                ("TranscribeAudioAsync", "transcription", true), ("TranscribeAudio", "transcription", false)
            ]),

            // Anthropic SDK
            ("Anthropic.AnthropicClient",
                [("CreateMessageAsync", "chat", true), ("CreateMessage", "chat", false)]),
            ("Anthropic.Messaging.MessageClient",
                [("CreateMessageAsync", "chat", true), ("CreateMessage", "chat", false)]),

            // OllamaSharp
            ("OllamaSharp.OllamaApiClient",
                [("ChatAsync", "chat", true), ("GenerateEmbeddingsAsync", "embeddings", true)]),

            // Azure.AI.OpenAI (legacy pattern)
            ("Azure.AI.OpenAI.OpenAIClient",
            [
                ("GetChatCompletionsAsync", "chat", true), ("GetChatCompletions", "chat", false),
                ("GetEmbeddingsAsync", "embeddings", true), ("GetEmbeddings", "embeddings", false)
            ]),

            // Microsoft Agent Framework (OpenTelemetryAgent excluded — already emits OTel spans)
            ("Microsoft.Agents.AI.AIAgent", agentMethods), ("Microsoft.Agents.AI.ChatClientAgent", agentMethods),
            ("Microsoft.Agents.AI.DelegatingAIAgent", agentMethods),

            // Cohere SDK
            ("Cohere.CohereClient",
                [("ChatAsync", "chat", true), ("EmbedAsync", "embeddings", true), ("RerankAsync", "rerank", true)])
        };

        var result =
            new List<(string MethodName, InvocationMatcher Matcher, string TypeMetadataName, string Operation, bool
                IsAsync)>();

        foreach (var (typeMetadataName, methods) in patterns)
        {
            foreach (var (methodName, operation, isAsync) in methods)
            {
                // Phase 1 only: method-name match via Invoke DSL
                // Phase 2 (type check via IsEqualTo) happens in TryMatchGenAiMethod
                result.Add((methodName, Invoke.Method(methodName), typeMetadataName, operation, isAsync));
            }
        }

        return [.. result];
    }
}
