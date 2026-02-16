using ANcpLua.Roslyn.Utilities;
using ANcpLua.Roslyn.Utilities.Matching;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find GenAI SDK method invocations to intercept.
/// </summary>
/// <remarks>
///     Patterns cover OTel Semantic Conventions v1.39 operations:
///     chat, embeddings, text_completion, image_generation, speech, transcription, rerank.
///     Uses the <see cref="Invoke" /> DSL from ANcpLua.Roslyn.Utilities for declarative matching.
/// </remarks>
internal static class GenAiCallSiteAnalyzer
{
    /// <summary>
    ///     Declarative GenAI method patterns.
    ///     Each entry pairs an <see cref="InvocationMatcher" /> with the OTel operation metadata it represents.
    /// </summary>
    private static readonly (InvocationMatcher Matcher, string Operation, bool IsAsync)[] Matchers =
        BuildMatchers();

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a GenAI invocation?
    ///     Delegates to <see cref="AnalyzerHelpers.CouldBeInvocation" />.
    /// </summary>
    public static bool CouldBeGenAiInvocation(SyntaxNode node, CancellationToken ct) =>
        AnalyzerHelpers.CouldBeInvocation(node, ct);

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

        if (!TryMatchGenAiMethod(invocation, out var provider, out var operation, out var isAsync))
            return null;

        // Skip if already intercepted by another generator
        if (AnalyzerHelpers.IsAlreadyIntercepted(context, cancellationToken))
            return null;

        var interceptLocation = context.SemanticModel.GetInterceptableLocation(
            (InvocationExpressionSyntax)context.Node,
            cancellationToken);

        if (interceptLocation is null)
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
        [NotNullWhen(true)] out string? provider,
        [NotNullWhen(true)] out string? operation,
        out bool isAsync)
    {
        provider = null;
        operation = null;
        isAsync = false;

        foreach (var (matcher, op, async) in Matchers)
        {
            if (!matcher.Matches(invocation))
                continue;

            var typeName = invocation.TargetMethod.ContainingType?.ToDisplayString();
            provider = (typeName is not null ? ProviderDetector.GetGenAiProviderId(typeName) : null) ?? "unknown";
            operation = op;
            isAsync = async;
            return true;
        }

        return false;
    }

    private static string? TryExtractModelName(IInvocationOperation invocation)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name is not ("model" or "modelId" or "deploymentName"))
                continue;

            if (argument.Value.ConstantValue is { HasValue: true, Value: string modelValue })
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
    private static (InvocationMatcher, string, bool)[] BuildMatchers()
    {
        // Agent framework methods shared across multiple types
        var agentMethods = new (string MethodName, string Operation, bool IsAsync)[]
        {
            ("RunAsync", "invoke_agent", true), ("RunStreamingAsync", "invoke_agent", true)
        };

        // (type prefix, methods) — same structure as the old MethodPatterns dictionary
        var patterns = new (string TypePrefix, (string MethodName, string Operation, bool IsAsync)[] Methods)[]
        {
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

            // Azure.AI.OpenAI (new pattern using OpenAI SDK) — no methods; uses OpenAI.Chat.ChatClient via GetChatClient()
            ("Azure.AI.OpenAI.AzureOpenAIClient", []),

            // Microsoft Agent Framework (OpenTelemetryAgent excluded — already emits OTel spans)
            ("Microsoft.Agents.AI.AIAgent", agentMethods), ("Microsoft.Agents.AI.ChatClientAgent", agentMethods),
            ("Microsoft.Agents.AI.DelegatingAIAgent", agentMethods),

            // Cohere SDK
            ("Cohere.CohereClient",
                [("ChatAsync", "chat", true), ("EmbedAsync", "embeddings", true), ("RerankAsync", "rerank", true)])
        };

        var result = new List<(InvocationMatcher, string, bool)>();

        foreach (var (typePrefix, methods) in patterns)
        {
            foreach (var (methodName, operation, isAsync) in methods)
            {
                var prefix = typePrefix; // capture for closure
                var matcher = Invoke.Method(methodName)
                    .Where(i => i.TargetMethod.ContainingType?.ToDisplayString()
                        .StartsWithIgnoreCase(prefix) == true);

                result.Add((matcher, operation, isAsync));
            }
        }

        return result.ToArray();
    }
}
