using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Analyzers;

/// <summary>
///     Analyzes syntax to find GenAI SDK method invocations to intercept.
/// </summary>
internal static class GenAiCallSiteAnalyzer
{
    /// <summary>
    ///     Known GenAI method patterns to intercept.
    ///     Key: containing type prefix, Value: (method name, operation name, is async).
    /// </summary>
    /// <remarks>
    ///     Patterns cover OTel Semantic Conventions v1.39 operations:
    ///     chat, embeddings, text_completion, image_generation, speech, transcription, rerank
    /// </remarks>
    /// <summary>
    ///     Shared method patterns for Microsoft Agent Framework types.
    ///     AIAgent, ChatClientAgent, and DelegatingAIAgent all expose the same RunAsync/RunStreamingAsync API.
    /// </summary>
    private static readonly (string MethodName, string Operation, bool IsAsync)[] AgentFrameworkMethods =
    [
        ("RunAsync", "invoke_agent", true),
        ("RunStreamingAsync", "invoke_agent", true)
    ];

    private static readonly Dictionary<string, (string MethodName, string Operation, bool IsAsync)[]> MethodPatterns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // OpenAI SDK v2.x
            ["OpenAI.Chat.ChatClient"] =
            [
                ("CompleteChatAsync", "chat", true),
                ("CompleteChat", "chat", false)
            ],
            ["OpenAI.Embeddings.EmbeddingClient"] =
            [
                ("GenerateEmbeddingsAsync", "embeddings", true),
                ("GenerateEmbeddings", "embeddings", false)
            ],
            ["OpenAI.Images.ImageClient"] =
            [
                ("GenerateImagesAsync", "image_generation", true),
                ("GenerateImages", "image_generation", false)
            ],
            ["OpenAI.Audio.AudioClient"] =
            [
                ("GenerateSpeechAsync", "speech", true),
                ("GenerateSpeech", "speech", false),
                ("TranscribeAudioAsync", "transcription", true),
                ("TranscribeAudio", "transcription", false)
            ],

            // Anthropic SDK
            ["Anthropic.AnthropicClient"] =
            [
                ("CreateMessageAsync", "chat", true),
                ("CreateMessage", "chat", false)
            ],
            ["Anthropic.Messaging.MessageClient"] =
            [
                ("CreateMessageAsync", "chat", true),
                ("CreateMessage", "chat", false)
            ],

            // OllamaSharp
            ["OllamaSharp.OllamaApiClient"] =
            [
                ("ChatAsync", "chat", true),
                ("GenerateEmbeddingsAsync", "embeddings", true)
            ],

            // Azure.AI.OpenAI (legacy pattern)
            ["Azure.AI.OpenAI.OpenAIClient"] =
            [
                ("GetChatCompletionsAsync", "chat", true),
                ("GetChatCompletions", "chat", false),
                ("GetEmbeddingsAsync", "embeddings", true),
                ("GetEmbeddings", "embeddings", false)
            ],

            // Azure.AI.OpenAI (new pattern using OpenAI SDK)
            ["Azure.AI.OpenAI.AzureOpenAIClient"] =
            [
                // Uses OpenAI.Chat.ChatClient pattern via GetChatClient()
            ],

            // Microsoft Agent Framework (OpenTelemetryAgent excluded — already emits OTel spans)
            ["Microsoft.Agents.AI.AIAgent"] = AgentFrameworkMethods,
            ["Microsoft.Agents.AI.ChatClientAgent"] = AgentFrameworkMethods,
            ["Microsoft.Agents.AI.DelegatingAIAgent"] = AgentFrameworkMethods,

            // Cohere SDK
            ["Cohere.CohereClient"] =
            [
                ("ChatAsync", "chat", true),
                ("EmbedAsync", "embeddings", true),
                ("RerankAsync", "rerank", true)
            ]
        };

    /// <summary>
    ///     Fast syntactic pre-filter: could this syntax node be a GenAI invocation?
    ///     Runs on every syntax node, so must be cheap (no semantic model).
    /// </summary>
    public static bool CouldBeGenAiInvocation(SyntaxNode node, CancellationToken _)
    {
        return node.IsKind(SyntaxKind.InvocationExpression);
    }

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
            method.Parameters.Select(static p => p.Type.ToDisplayString()).ToList(),
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

        var containingType = invocation.TargetMethod.ContainingType;
        if (containingType is null)
            return false;

        var typeName = containingType.ToDisplayString();
        var methodName = invocation.TargetMethod.Name;

        foreach (var kvp in MethodPatterns)
        {
            var typePrefix = kvp.Key;
            var methods = kvp.Value;

            if (!typeName.StartsWithOrdinal(typePrefix))
                continue;

            foreach (var methodPattern in methods)
            {
                if (methodName != methodPattern.MethodName)
                    continue;

                provider = ProviderDetector.GetGenAiProviderId(typeName) ?? "unknown";
                operation = methodPattern.Operation;
                isAsync = methodPattern.IsAsync;
                return true;
            }
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
}
