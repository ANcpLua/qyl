using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.Instrumentation.Generators.Models;

#region Provider Types

internal sealed record ProviderDefinition(
    string ProviderId,
    string TypeContains);

internal sealed record HostedServiceDefinition(
    string TypeFullyQualifiedName,
    string SortKey);

internal sealed record MapEndpointsDefinition(
    string ContainingTypeFullyQualifiedName,
    string MethodName,
    int Order,
    string SortKey);

internal sealed record QylServiceDefinition(
    string TypeFullyQualifiedName,
    string LifetimeMethodName,
    string? InterfaceFullyQualifiedName,
    string SortKey);

internal sealed record QylHealthCheckDefinition(
    string TypeFullyQualifiedName,
    string Name,
    EquatableArray<string> Tags,
    string SortKey);

internal static class ProviderRegistry
{
    public static readonly ImmutableArray<ProviderDefinition> GenAiProviders =
    [
        new("azure.ai.openai", "Azure.AI.OpenAI"),
        new("azure.ai.inference", "Azure.AI.Inference"),

        new("openai", "OpenAI"),

        new("anthropic", "Anthropic"),

        new("aws.bedrock", "Bedrock"),

        new("gcp.gemini", "GenerativeAI"),
        new("gcp.vertex_ai", "AIPlatform"),

        new("cohere", "Cohere"),
        new("mistral_ai", "Mistral"),
        new("groq", "Groq"),
        new("deepseek", "Deepseek"),
        new("perplexity", "Perplexity"),
        new("x_ai", "xAI"),

        new("microsoft_agents", "Agents.AI"),

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

internal enum BuilderCallKind
{
    Build
}

internal sealed record BuilderCallSite(
    string SortKey,
    BuilderCallKind Kind,
    InterceptableLocation Location);

#endregion

#region Database Call Site Types

internal enum DbCommandMethod
{
    ExecuteReader,
    ExecuteNonQuery,
    ExecuteScalar
}

internal sealed record DbCallSite(
    string SortKey,
    DbCommandMethod Method,
    bool IsAsync,
    string? ConcreteCommandType,
    InterceptableLocation Location);

#endregion

#region Meter Definition Types

internal sealed record MeterDefinition(
    string SortKey,
    string Namespace,
    EquatableArray<MeterContainingTypeDefinition> ContainingTypes,
    string ClassName,
    string ClassModifiers,
    string MeterName,
    string? MeterVersion,
    EquatableArray<MetricMethodDefinition> Methods);

internal sealed record MeterContainingTypeDefinition(
    string ClassName,
    string ClassModifiers);

internal enum MetricKind
{
    Counter,
    Histogram,
    Gauge,
    UpDownCounter,
    ObservableCounter,
    ObservableGauge,
    ObservableUpDownCounter
}

internal enum ObservableCallbackKind
{
    None,
    Value,
    Measurement,
    Measurements
}

internal sealed record MetricMethodDefinition(
    string MethodName,
    string Accessibility,
    MetricKind Kind,
    string MetricName,
    string? Unit,
    string? Description,
    string? ValueTypeName,
    string? ValueParameterName,
    ObservableCallbackKind CallbackKind,
    EquatableArray<MetricTagParameter> Tags);

internal sealed record MetricTagParameter(
    string ParameterName,
    string TagName,
    string TypeName);

#endregion

#region Traced Call Site Types

internal sealed record TracedCallSite(
    string SortKey,
    string ActivitySourceName,
    string SpanName,
    string SpanKind,
    bool RootSpan,
    string ContainingTypeName,
    string MethodName,
    bool IsStatic,
    bool IsAsync,
    bool IsAsyncEnumerable,
    string ReturnTypeName,
    EquatableArray<string> ParameterTypes,
    EquatableArray<string> ParameterNames,
    EquatableArray<TracedTagParameter> TracedTags,
    EquatableArray<TracedTagProperty> TracedTagProperties,
    EquatableArray<TypeParameterConstraint> TypeParameters,
    TracedReturnInfo? ReturnCapture,
    string? CodeFilePath,
    string? CodeNamespace,
    int CodeLineNumber,
    InterceptableLocation Location);

internal sealed record TracedTagParameter(
    string ParameterName,
    string TypeName,
    string TagName,
    bool SkipIfNull,
    bool SkipIfDefault,
    bool IsNullable,
    bool IsValueType);

internal sealed record TracedTagProperty(
    string PropertyName,
    string TagName,
    bool SkipIfNull,
    bool IsNullable,
    bool IsStatic);

internal sealed record TracedReturnInfo(
    string TagName,
    string? PropertyPath);

internal sealed record TypeParameterConstraint(
    string Name,
    string? Constraints);

#endregion
