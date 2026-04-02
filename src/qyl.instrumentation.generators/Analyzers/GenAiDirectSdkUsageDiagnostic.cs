using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Qyl.Instrumentation.Generators.Analyzers;

/// <summary>
///     Diagnostic analyzer that warns when application code calls GenAI SDK APIs directly,
///     bypassing the DI pipeline and OTel instrumentation layer.
/// </summary>
/// <remarks>
///     Fires <see cref="DirectSdkUsageRule" /> (QYL001) on every direct SDK invocation that
///     matches a known GenAI provider pattern — unless the call is on <c>IChatClient</c>,
///     which is already instrumented via <c>UseQylInstrumentation()</c>.
///
///     The fix: register the SDK client through DI and let the instrumentation middleware
///     handle OTel spans automatically:
///     <code>
///         builder.Services.AddChatClient(inner => new OpenAIChatClient(model, apiKey))
///                         .UseQylInstrumentation();
///     </code>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class GenAiDirectSdkUsageDiagnosticAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic ID for direct GenAI SDK usage without instrumentation middleware.</summary>
    public const string DiagnosticId = "QYL001";

    private const string Category = "Usage";

    private const string HelpLinkUri =
        "https://github.com/ANcpLua/qyl/blob/main/specs/instrumentation.md#genai-instrumentation";

    /// <summary>
    ///     QYL001: Direct SDK usage detected — instrumentation not applied automatically.
    /// </summary>
    public static readonly DiagnosticDescriptor DirectSdkUsageRule = new(
        DiagnosticId,
        title: "Direct GenAI SDK call bypasses automatic OTel instrumentation",
        messageFormat: "Direct {0} SDK call detected. Use builder.Services.AddChatClient(...).UseQylInstrumentation() for automatic OTel instrumentation.",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Calling a GenAI SDK type directly prevents automatic OpenTelemetry span emission. " +
                     "Wrap the provider client with AddChatClient().UseQylInstrumentation() in your " +
                     "DI registration so all LLM calls are traced without changes to call sites.",
        helpLinkUri: HelpLinkUri);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [DirectSdkUsageRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        // Skip generated files — they are infrastructure, not application code.
        var filePath = invocation.Syntax.SyntaxTree.FilePath;
        if (AnalyzerHelpers.IsGeneratedFile(filePath))
            return;

        // Fast name-based pre-filter before touching the semantic model.
        if (!GenAiCallSiteAnalyzer.CouldBeGenAiInvocation(invocation.Syntax, context.CancellationToken))
            return;

        var containingType = invocation.TargetMethod.ContainingType;
        if (containingType is null)
            return;

        // IChatClient calls are already instrumented via UseQylInstrumentation() — never warn.
        if (IsIChatClientMethod(containingType, context.Compilation))
            return;

        // Determine whether the containing type is a known direct-SDK type.
        var sdkName = ResolveSdkDisplayName(containingType, context.Compilation);
        if (sdkName is null)
            return;

        var diagnostic = Diagnostic.Create(
            DirectSdkUsageRule,
            invocation.Syntax.GetLocation(),
            sdkName);

        context.ReportDiagnostic(diagnostic);
    }

    private static bool IsIChatClientMethod(INamedTypeSymbol containingType, Compilation compilation)
    {
        var iChatClientType = compilation.GetTypeByMetadataName("Microsoft.Extensions.AI.IChatClient");
        return iChatClientType is not null && containingType.IsEqualTo(iChatClientType);
    }

    /// <summary>
    ///     Returns a short human-readable SDK name for the diagnostic message,
    ///     or null when the type is not a recognized direct-SDK type.
    /// </summary>
    private static string? ResolveSdkDisplayName(INamedTypeSymbol containingType, Compilation compilation)
    {
        // Direct SDK type metadata names and their display labels.
        // IChatClient is intentionally excluded — it is the instrumented abstraction.
        var sdkTypes = new (string MetadataName, string Label)[]
        {
            // OpenAI SDK v2.x
            ("OpenAI.Chat.ChatClient", "OpenAI Chat"),
            ("OpenAI.Embeddings.EmbeddingClient", "OpenAI Embeddings"),
            ("OpenAI.Images.ImageClient", "OpenAI Images"),
            ("OpenAI.Audio.AudioClient", "OpenAI Audio"),

            // Anthropic SDK
            ("Anthropic.AnthropicClient", "Anthropic"),
            ("Anthropic.Messaging.MessageClient", "Anthropic Messaging"),

            // OllamaSharp
            ("OllamaSharp.OllamaApiClient", "Ollama"),

            // Azure.AI.OpenAI (legacy)
            ("Azure.AI.OpenAI.OpenAIClient", "Azure.AI.OpenAI"),

            // Microsoft Agent Framework
            ("Microsoft.Agents.AI.AIAgent", "Microsoft.Agents.AI"),
            ("Microsoft.Agents.AI.ChatClientAgent", "Microsoft.Agents.AI"),
            ("Microsoft.Agents.AI.DelegatingAIAgent", "Microsoft.Agents.AI"),

            // Cohere SDK
            ("Cohere.CohereClient", "Cohere")
        };

        foreach (var (metadataName, label) in sdkTypes)
        {
            var expectedType = compilation.GetTypeByMetadataName(metadataName);
            if (expectedType is not null && containingType.IsEqualTo(expectedType))
                return label;
        }

        return null;
    }
}
