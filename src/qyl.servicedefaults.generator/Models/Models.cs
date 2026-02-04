using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.ServiceDefaults.Generator.Models;

#region Provider Types

/// <summary>
///     Defines an instrumentation provider.
/// </summary>
internal sealed record ProviderDefinition(
    string ProviderId,
    string TypeContains,
    TokenUsageDefinition? TokenUsage);

/// <summary>
///     Defines how to extract token usage from a response.
/// </summary>
internal sealed record TokenUsageDefinition(
    string InputProperty,
    string OutputProperty);

/// <summary>
///     Registry of GenAI instrumentation providers.
/// </summary>
/// <remarks>
///     Provider definitions for compile-time detection in the source generator.
///     Provider IDs use OTel Semantic Conventions v1.39 gen_ai.provider.name values.
///     See: https://opentelemetry.io/docs/specs/semconv/gen-ai/gen-ai-spans/
/// </remarks>
internal static class ProviderRegistry
{
    // OTel SemConv v1.39 gen_ai.provider.name values (inline for source generator compatibility)
    private const string OpenAi = "openai";
    private const string AzureAiOpenai = "azure.ai.openai";
    private const string AzureAiInference = "azure.ai.inference";
    private const string Anthropic = "anthropic";
    private const string AwsBedrock = "aws.bedrock";
    private const string GcpGemini = "gcp.gemini";
    private const string GcpVertexAi = "gcp.vertex_ai";
    private const string Cohere = "cohere";
    private const string MistralAi = "mistral_ai";
    private const string Groq = "groq";
    private const string Deepseek = "deepseek";
    private const string Perplexity = "perplexity";
    private const string XAi = "x_ai";
    private const string GithubCopilot = "github_copilot";

    /// <summary>
    ///     All GenAI providers with known SDK type patterns.
    /// </summary>
    /// <remarks>
    ///     Order matters: more specific patterns should come before generic ones.
    ///     Azure.AI.OpenAI must be checked before OpenAI to avoid incorrect matching.
    /// </remarks>
    public static readonly ImmutableArray<ProviderDefinition> GenAiProviders =
    [
        // Azure (must come before OpenAI due to type name overlap)
        new(AzureAiOpenai, "Azure.AI.OpenAI", new TokenUsageDefinition("Usage.InputTokens", "Usage.OutputTokens")),
        new(AzureAiInference, "Azure.AI.Inference", null),

        // OpenAI ecosystem (OpenAI SDK v2.x uses InputTokenCount/OutputTokenCount)
        new(OpenAi, "OpenAI", new TokenUsageDefinition("Usage.InputTokenCount", "Usage.OutputTokenCount")),

        // Anthropic
        new(Anthropic, "Anthropic", new TokenUsageDefinition("Usage.InputTokens", "Usage.OutputTokens")),

        // AWS
        new(AwsBedrock, "Bedrock", null),

        // Google
        new(GcpGemini, "GenerativeAI", null),
        new(GcpVertexAi, "AIPlatform", null),

        // Other providers
        new(Cohere, "Cohere", null),
        new(MistralAi, "Mistral", null),
        new(Groq, "Groq", new TokenUsageDefinition("Usage.PromptTokens", "Usage.CompletionTokens")),
        new(Deepseek, "Deepseek", null),
        new(Perplexity, "Perplexity", null),
        new(XAi, "xAI", null),

        // GitHub Copilot via Microsoft Agent Framework
        // AgentResponse.Usage is Microsoft.Extensions.AI.UsageDetails
        new(GithubCopilot, "Agents.AI", new TokenUsageDefinition("Usage.InputTokenCount", "Usage.OutputTokenCount")),

        // Custom providers (not in OTel semconv)
        new("ollama", "Ollama", null),
        new("together", "Together", null),
        new("replicate", "Replicate", null)
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
    IReadOnlyList<string> ParameterTypes,
    InterceptableLocation Location)
{
    /// <summary>
    ///     True if return type is IAsyncEnumerable (streaming response).
    /// </summary>
    public bool IsStreaming => ReturnTypeName.StartsWith("System.Collections.Generic.IAsyncEnumerable<", StringComparison.Ordinal);
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

#region OTel Tag Binding Types

/// <summary>
///     A discovered [OTel] attribute binding that maps a member to an Activity tag.
/// </summary>
/// <remarks>
///     The "Binding" suffix indicates the relationship between a code member
///     and its corresponding OTel tag name for Activity enrichment.
/// </remarks>
internal sealed record OTelTagBinding(
    string ContainingTypeName,
    string MemberName,
    string MemberTypeName,
    string AttributeName,
    bool SkipIfNull,
    bool IsNullable);

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
    IReadOnlyList<MetricMethodDefinition> Methods);

/// <summary>
///     The kind of metric instrument.
/// </summary>
internal enum MetricKind
{
    Counter,
    Histogram,
    Gauge
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
    IReadOnlyList<MetricTagParameter> Tags);

/// <summary>
///     A [Tag]-decorated parameter that becomes a metric dimension.
/// </summary>
internal sealed record MetricTagParameter(
    string ParameterName,
    string TagName,
    string TypeName);

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
    string ContainingTypeName,
    string MethodName,
    bool IsStatic,
    bool IsAsync,
    string ReturnTypeName,
    IReadOnlyList<string> ParameterTypes,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<TracedTagParameter> TracedTags,
    IReadOnlyList<TypeParameterConstraint> TypeParameters,
    InterceptableLocation Location);

/// <summary>
///     A [TracedTag]-decorated parameter that becomes a span attribute.
/// </summary>
internal sealed record TracedTagParameter(
    string ParameterName,
    string TagName,
    bool SkipIfNull,
    bool IsNullable);

/// <summary>
///     Type parameter with its constraints for generic method interception.
/// </summary>
internal sealed record TypeParameterConstraint(
    string Name,
    string? Constraints);

#endregion
