// =============================================================================
// qyl.instrumentation.generators - GenAI Interceptor Generator
// Compile-time auto-instrumentation using C# interceptors
// Owner: qyl.instrumentation.generators
// =============================================================================

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using qyl.instrumentation.generators.Emitters;

namespace qyl.instrumentation.generators.Interceptors;

/// <summary>
/// Source generator that creates interceptors for GenAI/LLM calls.
///
/// Architecture:
/// - Interception = HOW you capture data (interceptors wrap method calls)
/// - Semantic conventions = WHAT you capture (OTel GenAI semconv from qyl.protocol)
///
/// Interceptor Limitations (be honest):
/// - Cannot instrument interface calls (IOpenAiClient.Chat)
/// - Cannot instrument virtual dispatch (base class calls)
/// - Cannot instrument compiled packages (only YOUR source code)
///
/// What OTel libraries already do (don't reinvent):
/// - HTTP client/server, Database, gRPC, Runtime metrics
///
/// What this generator covers (gaps OTel doesn't fill):
/// - GenAI/LLM SDK calls (OpenAI, Anthropic, Ollama, Azure AI)
/// </summary>
[Generator]
public sealed class GenAiInterceptorGenerator : IIncrementalGenerator
{
    // Target methods we know how to instrument
    private static readonly Dictionary<string, GenAiTarget> KnownTargets = new()
    {
        ["OpenAI.Chat.ChatClient.CompleteChatAsync"] = new("openai", "chat", "chat {gen_ai.request.model}"),
        ["OpenAI.Chat.ChatClient.CompleteChat"] = new("openai", "chat", "chat {gen_ai.request.model}"),
        ["Anthropic.AnthropicClient.CreateMessageAsync"] = new("anthropic", "chat", "chat {gen_ai.request.model}"),
        ["Anthropic.Messages.MessageClient.CreateAsync"] = new("anthropic", "chat", "chat {gen_ai.request.model}"),
        ["OllamaSharp.OllamaApiClient.ChatAsync"] = new("ollama", "chat", "chat {gen_ai.request.model}"),
        ["Azure.AI.Inference.ChatCompletionsClient.CompleteAsync"] = new("az.ai.inference", "chat", "chat {gen_ai.request.model}"),
        ["Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService.GetChatMessageContentsAsync"] = new("semantic_kernel", "chat", "chat {gen_ai.request.model}"),
    };

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all method invocations that match our targets
        var invocations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is InvocationExpressionSyntax,
                transform: static (ctx, ct) => GetInterceptTarget(ctx, ct))
            .Where(static target => target is not null)
            .Select(static (target, _) => target!.Value);

        // Collect and emit
        context.RegisterSourceOutput(
            invocations.Collect(),
            static (spc, targets) => Execute(spc, targets));

        // Always emit the configuration source
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource("GenAiInstrumentationConfig.g.cs", SourceText.From(ConfigSource, Encoding.UTF8));
        });
    }

    private static InterceptorTarget? GetInterceptTarget(
        GeneratorSyntaxContext ctx,
        CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol method)
            return null;

        var containingType = method.ContainingType?.ToDisplayString() ?? "";
        var key = $"{containingType}.{method.Name}";

        if (!KnownTargets.TryGetValue(key, out var target))
            return null;

        var location = invocation.GetLocation();
        var lineSpan = location.GetLineSpan();

        var parameters = method.Parameters
            .Select(static p => new ParameterInfo(p.Type.ToDisplayString(), p.Name))
            .ToArray();

        return new InterceptorTarget(
            FilePath: lineSpan.Path,
            Line: lineSpan.StartLinePosition.Line + 1,
            Column: lineSpan.StartLinePosition.Character + 1,
            ContainingType: containingType,
            MethodName: method.Name,
            ReturnType: method.ReturnType.ToDisplayString(),
            IsAsync: method.IsAsync || method.ReturnType.Name.Contains("Task"),
            Provider: target.Provider,
            Operation: target.Operation,
            SpanNameTemplate: target.SpanNameTemplate,
            Parameters: parameters);
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<InterceptorTarget> targets)
    {
        if (targets.IsDefaultOrEmpty)
            return;

        var source = InterceptorEmitter.Emit(targets.ToArray());
        context.AddSource("GenAiInterceptors.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    private const string ConfigSource = """
        // <auto-generated/>
        // Generated by qyl.instrumentation.generators

        namespace qyl.instrumentation;

        /// <summary>
        /// Configuration for GenAI auto-instrumentation.
        /// </summary>
        public static class GenAiInstrumentationConfig
        {
            /// <summary>
            /// ActivitySource name for GenAI spans.
            /// Add to your OTel configuration:
            /// <code>
            /// builder.Services.AddOpenTelemetry()
            ///     .WithTracing(t => t.AddSource(GenAiInstrumentationConfig.ActivitySourceName));
            /// </code>
            /// </summary>
            public const string ActivitySourceName = "qyl.instrumentation.GenAi";

            /// <summary>OTel schema version.</summary>
            public const string SchemaUrl = "https://opentelemetry.io/schemas/1.39.0";
        }
        """;
}

internal readonly record struct GenAiTarget(string Provider, string Operation, string SpanNameTemplate);

internal readonly record struct InterceptorTarget(
    string FilePath,
    int Line,
    int Column,
    string ContainingType,
    string MethodName,
    string ReturnType,
    bool IsAsync,
    string Provider,
    string Operation,
    string SpanNameTemplate,
    ParameterInfo[] Parameters);

internal readonly record struct ParameterInfo(string Type, string Name);
