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
/// </remarks>
internal static class ProviderRegistry
{
    /// <summary>
    ///     All GenAI providers.
    /// </summary>
    public static readonly ImmutableArray<ProviderDefinition> GenAiProviders =
    [
        new("openai", "OpenAI", new TokenUsageDefinition("Usage.InputTokenCount", "Usage.OutputTokenCount")),
        new("anthropic", "Anthropic", new TokenUsageDefinition("Usage.InputTokens", "Usage.OutputTokens")),
        new("azure_openai", "Azure.AI.OpenAI", new TokenUsageDefinition("Usage.PromptTokens", "Usage.CompletionTokens")),
        new("ollama", "Ollama", null),
        new("google_ai", "GenerativeAI", null),
        new("vertex_ai", "AIPlatform", null)
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
    InterceptableLocation InterceptableLocation);

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
