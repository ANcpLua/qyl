using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using ANcpLua.Agents.Instrumentation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Qyl.Instrumentation.Instrumentation.Loom;

/// <summary>
///     Converts <see cref="LoomRuntimeMetadataDescriptor"/> into <see cref="AIFunction"/> instances
///     via <see cref="AIFunctionFactory"/>, bypassing the custom <c>LoomToolAIFunction</c> subclass.
///     All binding metadata is compiler-emitted; the bridge performs no runtime discovery.
/// </summary>
public static class LoomToolFactoryBridge
{
    private static readonly ConcurrentDictionary<(Type, string), MethodInfo> MethodInfoCache = new();

    public static AIFunction CreateAIFunction(
        LoomRuntimeMetadataDescriptor metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return CreateCore(metadata, BuildDescriptionFromMetadata(metadata), services);
    }

    public static AIFunction CreateAIFunction(
        LoomToolDescriptor descriptor,
        LoomRuntimeMetadataDescriptor metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(metadata);
        return CreateCore(metadata, BuildDescriptionFromDescriptor(descriptor), services);
    }

    public static IReadOnlyList<AIFunction> CreateAIFunctions(
        IEnumerable<LoomRuntimeMetadataDescriptor> metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.Select(m => CreateAIFunction(m, services)).ToArray();
    }

    public static IReadOnlyList<AIFunction> CreateAIFunctions(
        IEnumerable<(LoomToolDescriptor Descriptor, LoomRuntimeMetadataDescriptor Metadata)> pairs,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(p => CreateAIFunction(p.Descriptor, p.Metadata, services)).ToArray();
    }

    public static AIFunction CreateInstrumentedAIFunction(
        LoomRuntimeMetadataDescriptor metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new TracedAIFunction(CreateAIFunction(metadata, services), ActivitySources.GenAiSource);
    }

    public static AIFunction CreateInstrumentedAIFunction(
        LoomToolDescriptor descriptor,
        LoomRuntimeMetadataDescriptor metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(metadata);
        return new TracedAIFunction(CreateAIFunction(descriptor, metadata, services), ActivitySources.GenAiSource);
    }

    public static IReadOnlyList<AIFunction> CreateInstrumentedAIFunctions(
        IEnumerable<LoomRuntimeMetadataDescriptor> metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return metadata.Select(m => CreateInstrumentedAIFunction(m, services)).ToArray();
    }

    public static IReadOnlyList<AIFunction> CreateInstrumentedAIFunctions(
        IEnumerable<(LoomToolDescriptor Descriptor, LoomRuntimeMetadataDescriptor Metadata)> pairs,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(p => CreateInstrumentedAIFunction(p.Descriptor, p.Metadata, services)).ToArray();
    }

    private static AIFunction CreateCore(
        LoomRuntimeMetadataDescriptor metadata,
        string description,
        IServiceProvider? services)
    {
        var methodInfo = MethodInfoCache.GetOrAdd((metadata.DeclaringType, metadata.MethodName), static key =>
            key.Item1.GetMethod(
                key.Item2,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Method '{key.Item2}' not found on type '{key.Item1.FullName ?? key.Item1.Name}'."));

        var options = new AIFunctionFactoryOptions
        {
            Name = metadata.Name,
            Description = description,
            ConfigureParameterBinding = paramInfo => ConfigureParameterBinding(paramInfo, metadata),
            MarshalResult = BuildMarshalResult(metadata.Result),
            ExcludeResultSchema = !metadata.Result.IsSchemaVisible,
            AdditionalProperties = BuildAdditionalProperties(metadata)
        };

        if (methodInfo.IsStatic)
            return AIFunctionFactory.Create(methodInfo, (object?)null, options);

        Func<AIFunctionArguments, object> instanceFactory = args =>
        {
            var sp = args.Services ?? services ?? throw new InvalidOperationException(
                $"No IServiceProvider available to resolve {metadata.DeclaringType.Name} for Loom tool '{metadata.Name}'.");
            return sp.GetRequiredService(metadata.DeclaringType);
        };

        return AIFunctionFactory.Create(methodInfo, instanceFactory, options);
    }

    private static AIFunctionFactoryOptions.ParameterBindingOptions ConfigureParameterBinding(
        ParameterInfo paramInfo,
        LoomRuntimeMetadataDescriptor metadata)
    {
        var binding = metadata.ParameterBindings.FirstOrDefault(b =>
            string.Equals(b.Name, paramInfo.Name, StringComparison.Ordinal));

        if (binding is null || (!binding.IsInfrastructureBound && binding.IsSchemaVisible))
            return default;

        // CancellationToken is handled natively by MEAI -- exclude from schema only, no custom binder.
        if (binding.Type == typeof(CancellationToken))
            return new AIFunctionFactoryOptions.ParameterBindingOptions { ExcludeFromSchema = true };

        return new AIFunctionFactoryOptions.ParameterBindingOptions
        {
            ExcludeFromSchema = true,
            BindParameter = binding.Type == typeof(IServiceProvider)
                ? static (_, args) => args.Services
                : static (pi, args) => args.Services?.GetService(pi.ParameterType)
        };
    }

    private static Func<object?, Type?, CancellationToken, ValueTask<object?>>? BuildMarshalResult(
        LoomResultDescriptor result)
    {
        if (!result.HasStructuredOutput || result.StructuredOutputType is null)
            return null;

        var structuredType = result.StructuredOutputType;
        return (value, _, _) => value is null
            ? new ValueTask<object?>((object?)null)
            : new ValueTask<object?>((object?)JsonSerializer.SerializeToElement(value, structuredType));
    }

    private static string BuildDescriptionFromMetadata(LoomRuntimeMetadataDescriptor metadata) =>
        $"[{metadata.Phase}] {metadata.Name}";

    private static string BuildDescriptionFromDescriptor(LoomToolDescriptor descriptor) =>
        string.Join(' ', new[]
        {
            descriptor.Description,
            descriptor.UseOnlyWhen is not null ? $"Use only when {descriptor.UseOnlyWhen}." : null,
            descriptor.DoNotUseWhen is not null ? $"Do not use when {descriptor.DoNotUseWhen}." : null
        }.Where(static s => !string.IsNullOrWhiteSpace(s)));

    private static Dictionary<string, object?> BuildAdditionalProperties(
        LoomRuntimeMetadataDescriptor metadata) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["loom.name"] = metadata.Name,
            ["loom.declaringType"] = metadata.DeclaringType.FullName ?? metadata.DeclaringType.Name,
            ["loom.methodName"] = metadata.MethodName,
            ["loom.phase"] = metadata.Phase.ToString(),
            ["loom.bridge"] = "factory",

            ["loom.binding.parameters"] = metadata.ParameterBindings.Select(static b => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = b.Name,
                ["type"] = b.Type.FullName ?? b.Type.Name,
                ["schemaVisible"] = b.IsSchemaVisible,
                ["infrastructureBound"] = b.IsInfrastructureBound,
                ["nullable"] = b.IsNullable
            }).ToArray(),

            ["loom.result.outputType"] = metadata.Result.OutputType?.FullName,
            ["loom.result.structuredOutputType"] = metadata.Result.StructuredOutputType?.FullName,
            ["loom.result.schemaHint"] = metadata.Result.ResultSchemaHint,
            ["loom.result.hasStructuredOutput"] = metadata.Result.HasStructuredOutput,
            ["loom.result.schemaVisible"] = metadata.Result.IsSchemaVisible,

            ["loom.telemetry.isAwaitable"] = metadata.Telemetry.IsAwaitable,
            ["loom.telemetry.returnsValue"] = metadata.Telemetry.ReturnsValue,
            ["loom.telemetry.sideEffect"] = metadata.Telemetry.SideEffect.ToString(),
            ["loom.telemetry.capabilities"] = metadata.Telemetry.RequiredCapabilities.ToArray(),

            ["loom.policy.requiresApproval"] = metadata.Policy.RequiresApproval,
            ["loom.policy.sideEffect"] = metadata.Policy.SideEffect.ToString(),
            ["loom.policy.maxAttempts"] = metadata.Policy.MaxAttempts,
            ["loom.policy.maxToolCalls"] = metadata.Policy.MaxToolCalls,
            ["loom.policy.maxTokens"] = metadata.Policy.MaxTokens,
            ["loom.policy.capabilities"] = metadata.Policy.RequiredCapabilities.ToArray()
        };
}
