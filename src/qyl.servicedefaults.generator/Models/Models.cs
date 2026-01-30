using System.Collections.Immutable;
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
    public static readonly ImmutableArray<ProviderDefinition> GenAiProviders =
    [
        // OpenAI ecosystem
        new(OpenAi, "OpenAI", new TokenUsageDefinition("Usage.InputTokenCount", "Usage.OutputTokenCount")),
        new(AzureAiOpenai, "Azure.AI.OpenAI", new TokenUsageDefinition("Usage.PromptTokens", "Usage.CompletionTokens")),
        new(AzureAiInference, "Azure.AI.Inference", null),

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
        new(Groq, "Groq", null),
        new(Deepseek, "Deepseek", null),
        new(Perplexity, "Perplexity", null),
        new(XAi, "xAI", null),

        // GitHub Copilot via Microsoft Agent Framework
        // AgentResponse.Usage is Microsoft.Extensions.AI.UsageDetails
        new(GithubCopilot, "Agents.AI", new TokenUsageDefinition("Usage.InputTokenCount", "Usage.OutputTokenCount")),

        // Custom providers (not in OTel semconv)
        new("ollama", "Ollama", null)
    ];
}

#endregion

#region Interception Types

/// <summary>
///     The kind of method interception.
/// </summary>
internal enum InterceptionMethodKind
{
    Build
}

/// <summary>
///     Represents a method invocation to be intercepted.
/// </summary>
internal sealed record InterceptionData(
    string OrderKey,
    InterceptionMethodKind Kind,
    InterceptableLocation InterceptableLocation);

#endregion

#region GenAI Types

/// <summary>
///     Represents a GenAI SDK method invocation to be intercepted.
/// </summary>
internal sealed record GenAiInvocationInfo(
    string OrderKey,
    string Provider,
    string Operation,
    string? Model,
    string ContainingTypeName,
    string MethodName,
    bool IsAsync,
    string ReturnTypeName,
    IReadOnlyList<string> ParameterTypes,
    InterceptableLocation InterceptableLocation)
{
    /// <summary>
    ///     Indicates if the return type is a streaming enumerable (IAsyncEnumerable).
    /// </summary>
    public bool IsStreaming => ReturnTypeName.StartsWith("System.Collections.Generic.IAsyncEnumerable<", StringComparison.Ordinal);
}

#endregion

#region Database Types

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
///     Represents an ADO.NET DbCommand method invocation to be intercepted.
/// </summary>
internal sealed record DbInvocationInfo(
    string OrderKey,
    DbCommandMethod Method,
    bool IsAsync,
    string? ConcreteCommandType,
    InterceptableLocation InterceptableLocation);

#endregion

#region OTel Types

/// <summary>
///     Information about a type member decorated with [OTel] attribute.
/// </summary>
internal sealed record OTelTagInfo(
    string ContainingTypeName,
    string MemberName,
    string MemberTypeName,
    string AttributeName,
    bool SkipIfNull,
    bool IsNullable);

#endregion

#region Meter Types

/// <summary>
///     Information about a class decorated with [Meter] attribute.
/// </summary>
internal sealed record MeterClassInfo(
    string OrderKey,
    string Namespace,
    string ClassName,
    string MeterName,
    string? MeterVersion,
    IReadOnlyList<MetricMethodInfo> Methods);

/// <summary>
///     The kind of metric instrument.
/// </summary>
internal enum MetricKind
{
    Counter,
    Histogram
}

/// <summary>
///     Information about a metric method.
/// </summary>
internal sealed record MetricMethodInfo(
    string MethodName,
    MetricKind Kind,
    string MetricName,
    string? Unit,
    string? Description,
    string? ValueTypeName,
    IReadOnlyList<MetricTagInfo> Tags);

/// <summary>
///     Information about a metric tag parameter.
/// </summary>
internal sealed record MetricTagInfo(
    string ParameterName,
    string TagName,
    string TypeName);

#endregion

#region Traced Types

/// <summary>
///     Represents a method decorated with [Traced] attribute to be intercepted.
/// </summary>
internal sealed record TracedInvocationInfo(
    string OrderKey,
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
    IReadOnlyList<TracedTagInfo> TracedTags,
    IReadOnlyList<TypeParameterInfo> TypeParameters,
    InterceptableLocation InterceptableLocation);

/// <summary>
///     Information about a parameter decorated with [TracedTag] attribute.
/// </summary>
internal sealed record TracedTagInfo(
    string ParameterName,
    string TagName,
    bool SkipIfNull,
    bool IsNullable);

/// <summary>
///     Information about a type parameter on a generic method.
/// </summary>
internal sealed record TypeParameterInfo(
    string Name,
    string? Constraints);

#endregion
