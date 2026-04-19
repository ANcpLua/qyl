using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.Instrumentation.Generators.Models;

#region Provider Types

/// <summary>
///     Maps a GenAI provider's SDK type-name substring to its OTel provider ID.
///     Used by <see cref="CallSites.ProviderDetector" /> for compile-time capability discovery.
/// </summary>
internal sealed record ProviderDefinition(
    string ProviderId,
    string TypeContains);

/// <summary>
///     A class tagged <c>[QylHostedService]</c> that will be auto-registered via
///     <c>services.AddHostedService&lt;T&gt;()</c> in the generated <c>QylGeneratedRegistry</c>.
/// </summary>
internal sealed record HostedServiceDefinition(
    string TypeFullyQualifiedName,
    string SortKey);

/// <summary>
///     A static extension method tagged <c>[QylMapEndpoints]</c> that the generator
///     dispatches from <c>QylGeneratedRegistry.MapQylGeneratedEndpoints</c>.
/// </summary>
internal sealed record MapEndpointsDefinition(
    string ContainingTypeFullyQualifiedName,
    string MethodName,
    int Order,
    string SortKey);

/// <summary>
///     Registry of GenAI providers for compile-time capability discovery.
/// </summary>
/// <remarks>
///     Provider IDs use OTel Semantic Conventions v1.40 gen_ai.provider.name values.
///     Order matters: more specific patterns must come before generic ones
///     (Azure.AI.OpenAI before OpenAI).
/// </remarks>
internal static class ProviderRegistry
{
    public static readonly ImmutableArray<ProviderDefinition> GenAiProviders =
    [
        // Azure (must come before OpenAI due to type name overlap)
        new("azure.ai.openai", "Azure.AI.OpenAI"),
        new("azure.ai.inference", "Azure.AI.Inference"),

        // OpenAI
        new("openai", "OpenAI"),

        // Anthropic
        new("anthropic", "Anthropic"),

        // AWS
        new("aws.bedrock", "Bedrock"),

        // Google
        new("gcp.gemini", "GenerativeAI"),
        new("gcp.vertex_ai", "AIPlatform"),

        // Other providers
        new("cohere", "Cohere"),
        new("mistral_ai", "Mistral"),
        new("groq", "Groq"),
        new("deepseek", "Deepseek"),
        new("perplexity", "Perplexity"),
        new("x_ai", "xAI"),

        // Microsoft Agent Framework
        new("microsoft_agents", "Agents.AI"),

        // Community providers
        new("ollama", "Ollama"),
        new("together_ai", "Together"),
        new("replicate", "Replicate"),
        new("meta", "Meta"),
        new("huggingface", "HuggingFace"),
        new("fireworks", "Fireworks"),
        new("anyscale", "Anyscale")
    ];
}

#endregion

#region ASP.NET Builder Call Site Types

/// <summary>
///     The kind of ASP.NET Core builder call being intercepted.
/// </summary>
internal enum BuilderCallKind
{
    /// <summary>WebApplicationBuilder.Build() - the final build step.</summary>
    Build
}

/// <summary>
///     A discovered call site for ASP.NET builder methods.
/// </summary>
/// <remarks>
///     "CallSite" follows Roslyn terminology: WHERE in code the call occurs.
///     SortKey ensures deterministic code generation across compilations.
/// </remarks>
internal sealed record BuilderCallSite(
    string SortKey,
    BuilderCallKind Kind,
    InterceptableLocation Location);

#endregion

#region GenAI Call Site Types

/// <summary>
///     A discovered GenAI SDK call site ready for instrumentation.
/// </summary>
/// <remarks>
///     Contains complete semantic information extracted from a GenAI method call.
///     This enables generating OTel-compliant spans with proper gen_ai.* attributes.
/// </remarks>
internal sealed record GenAiCallSite(
    string SortKey,
    string Provider,
    string Operation,
    string? Model,
    string ContainingTypeName,
    string MethodName,
    bool IsAsync,
    string ReturnTypeName,
    EquatableArray<string> ParameterTypes,
    InterceptableLocation Location)
{
    /// <summary>
    ///     True if return type is IAsyncEnumerable (streaming response).
    /// </summary>
    public bool IsStreaming => ReturnTypeName.StartsWithOrdinal("System.Collections.Generic.IAsyncEnumerable<");
}

#endregion

#region Database Call Site Types

/// <summary>
///     The ADO.NET DbCommand methods that can be intercepted.
/// </summary>
internal enum DbCommandMethod
{
    ExecuteReader,
    ExecuteNonQuery,
    ExecuteScalar
}

/// <summary>
///     A discovered database call site ready for instrumentation.
/// </summary>
internal sealed record DbCallSite(
    string SortKey,
    DbCommandMethod Method,
    bool IsAsync,
    string? ConcreteCommandType,
    InterceptableLocation Location);

#endregion

#region Meter Definition Types

/// <summary>
///     A discovered [Meter]-decorated class defining custom metrics.
/// </summary>
/// <remarks>
///     The "Definition" suffix indicates this describes WHAT to generate,
///     not WHERE a call occurs (unlike "CallSite").
/// </remarks>
internal sealed record MeterDefinition(
    string SortKey,
    string Namespace,
    string ClassName,
    string MeterName,
    string? MeterVersion,
    EquatableArray<MetricMethodDefinition> Methods);

/// <summary>
///     The kind of metric instrument.
/// </summary>
internal enum MetricKind
{
    Counter,
    Histogram,
    Gauge,
    UpDownCounter
}

/// <summary>
///     A partial method that defines a metric recording operation.
/// </summary>
internal sealed record MetricMethodDefinition(
    string MethodName,
    MetricKind Kind,
    string MetricName,
    string? Unit,
    string? Description,
    string? ValueTypeName,
    EquatableArray<MetricTagParameter> Tags);

/// <summary>
///     A [Tag]-decorated parameter that becomes a metric dimension.
/// </summary>
internal sealed record MetricTagParameter(
    string ParameterName,
    string TagName,
    string TypeName);

#endregion

#region Agent Call Site Types

/// <summary>
///     The kind of Microsoft.Agents.AI call being intercepted.
/// </summary>
internal enum AgentCallKind
{
    /// <summary>AIAgent.InvokeAsync / ChatClientAgent.InvokeAsync</summary>
    InvokeAsync,

    /// <summary>AIAgentBuilder.AddAgent / UseAgent / ConfigureAgent</summary>
    BuilderRegistration,

    /// <summary>[AgentTraced]-decorated method</summary>
    AgentTracedMethod
}

/// <summary>
///     A discovered Microsoft.Agents.AI call site ready for instrumentation.
/// </summary>
internal sealed record AgentCallSite(
    string SortKey,
    string? AgentName,
    AgentCallKind Kind,
    string ContainingTypeName,
    string MethodName,
    bool IsAsync,
    string ReturnTypeName,
    EquatableArray<string> ParameterTypes,
    EquatableArray<string> ParameterNames,
    InterceptableLocation Location);

#endregion

#region Traced Call Site Types

/// <summary>
///     A discovered call site for a [Traced]-decorated method.
/// </summary>
internal sealed record TracedCallSite(
    string SortKey,
    string ActivitySourceName,
    string SpanName,
    string SpanKind,
    // T-001: When true the span is created without a parent (root span).
    bool RootSpan,
    string ContainingTypeName,
    string MethodName,
    bool IsStatic,
    bool IsAsync,
    // T-002: True when return type is IAsyncEnumerable<T>.
    bool IsAsyncEnumerable,
    string ReturnTypeName,
    EquatableArray<string> ParameterTypes,
    EquatableArray<string> ParameterNames,
    EquatableArray<TracedTagParameter> TracedTags,
    // T-004: Properties on the containing type decorated with [TracedTag].
    EquatableArray<TracedTagProperty> TracedTagProperties,
    EquatableArray<TypeParameterConstraint> TypeParameters,
    // T-007: Return-value capture descriptor, or null if not requested.
    TracedReturnInfo? ReturnCapture,
    // T-008: OTel code.* attributes — source location of the [Traced] method definition.
    string? CodeFilePath,
    string? CodeNamespace,
    int CodeLineNumber,
    InterceptableLocation Location);

/// <summary>
///     A [TracedTag]-decorated parameter that becomes a span attribute.
/// </summary>
internal sealed record TracedTagParameter(
    string ParameterName,
    // Fully-qualified type name — needed for T-006 SkipIfDefault EqualityComparer.
    string TypeName,
    string TagName,
    bool SkipIfNull,
    // T-006: Skip tag when value equals default(T).
    bool SkipIfDefault,
    bool IsNullable,
    bool IsValueType);

/// <summary>
///     T-004: A [TracedTag]-decorated property on the containing type.
/// </summary>
internal sealed record TracedTagProperty(
    string PropertyName,
    string TagName,
    bool SkipIfNull,
    bool IsNullable,
    bool IsStatic);

/// <summary>
///     T-007: Captures the return value as a span attribute.
/// </summary>
internal sealed record TracedReturnInfo(
    string TagName,
    // Optional dotted member path e.g. "Usage.InputTokens". Null → ToString().
    string? PropertyPath);

/// <summary>
///     Type parameter with its constraints for generic method interception.
/// </summary>
internal sealed record TypeParameterConstraint(
    string Name,
    string? Constraints);

#endregion

#region Tool Manifest Types

/// <summary>
///     A discovered [McpServerToolType]-decorated class for compile-time tool registration.
/// </summary>
internal sealed record ToolTypeEntry(
    string SortKey,
    string FullyQualifiedTypeName);

#endregion
