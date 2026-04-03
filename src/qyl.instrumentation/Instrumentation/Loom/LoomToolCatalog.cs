using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.Loom;

public static partial class LoomGeneratedRegistry
{
    public static IReadOnlyList<AIFunction> ToAIFunctions(IServiceProvider? services = null)
        => Tools.ToAIFunctions(services);

    public static IReadOnlyList<AIFunction> ToAIFunctions(
        LoomToolAIFunctionOptions options,
        IServiceProvider? services = null)
        => Tools.ToAIFunctions(options, services);

    public static IReadOnlyList<AITool> ToToolCatalog(IServiceProvider? services = null)
        => Tools.ToToolCatalog(services);

    public static IReadOnlyList<AITool> ToToolCatalog(
        LoomToolAIFunctionOptions options,
        IServiceProvider? services = null)
        => Tools.ToToolCatalog(options, services);

    public static IReadOnlyList<AIFunction> ToFactoryAIFunctions(IServiceProvider? services = null)
        => LoomToolFactoryBridge.CreateAIFunctions(RuntimeMetadata, services);

    public static IReadOnlyList<AIFunction> ToInstrumentedFactoryAIFunctions(IServiceProvider? services = null)
        => LoomToolFactoryBridge.CreateInstrumentedAIFunctions(RuntimeMetadata, services);

    public static IReadOnlyList<AIFunction> ToPairedFactoryAIFunctions(IServiceProvider? services = null)
        => LoomToolFactoryBridge.CreateAIFunctions(
            Tools.Zip(RuntimeMetadata, static (d, m) => (d, m)), services);
}

public static class LoomToolDescriptorExtensions
{
    public static LoomToolBindingSurface GetBindingSurface(
        this LoomToolDescriptor descriptor,
        LoomToolAIFunctionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        options ??= LoomToolAIFunctionOptions.Default;

        var parameterBindings = descriptor.Parameters
            .Select(parameter => BindParameter(parameter, options))
            .ToArray();

        return new LoomToolBindingSurface(parameterBindings, BindResult(descriptor, options));
    }

    public static AIFunction ToAIFunction(
        this LoomToolDescriptor descriptor,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new LoomToolAIFunction(descriptor, LoomToolAIFunctionOptions.Default, services);
    }

    public static AIFunction ToAIFunction(
        this LoomToolDescriptor descriptor,
        LoomToolAIFunctionOptions options,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        return new LoomToolAIFunction(descriptor, options, services);
    }

    public static IReadOnlyList<AIFunction> ToAIFunctions(
        this IEnumerable<LoomToolDescriptor> descriptors,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        return descriptors.Select(descriptor => descriptor.ToAIFunction(services)).ToArray();
    }

    public static IReadOnlyList<AIFunction> ToAIFunctions(
        this IEnumerable<LoomToolDescriptor> descriptors,
        LoomToolAIFunctionOptions options,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(options);
        return descriptors.Select(descriptor => descriptor.ToAIFunction(options, services)).ToArray();
    }

    public static IReadOnlyList<AITool> ToToolCatalog(
        this IEnumerable<LoomToolDescriptor> descriptors,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        return descriptors.Select(descriptor => (AITool)descriptor.ToAIFunction(services)).ToArray();
    }

    public static IReadOnlyList<AITool> ToToolCatalog(
        this IEnumerable<LoomToolDescriptor> descriptors,
        LoomToolAIFunctionOptions options,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        ArgumentNullException.ThrowIfNull(options);
        return descriptors.Select(descriptor => (AITool)descriptor.ToAIFunction(options, services)).ToArray();
    }

    public static AIFunction ToFactoryAIFunction(
        this LoomToolDescriptor descriptor,
        LoomRuntimeMetadataDescriptor metadata,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(metadata);
        return LoomToolFactoryBridge.CreateAIFunction(descriptor, metadata, services);
    }

    public static IReadOnlyList<AIFunction> ToFactoryAIFunctions(
        this IEnumerable<(LoomToolDescriptor Descriptor, LoomRuntimeMetadataDescriptor Metadata)> pairs,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(p => LoomToolFactoryBridge.CreateAIFunction(p.Descriptor, p.Metadata, services)).ToArray();
    }

    private static LoomToolParameterBindingDescriptor BindParameter(
        LoomToolParameterDescriptor parameter,
        LoomToolAIFunctionOptions options)
    {
        var binding = options.BindParameter?.Invoke(parameter) ?? CreateDefaultParameterBinding(parameter);

        return binding with
        {
            Parameter = parameter
        };
    }

    private static LoomToolResultBindingDescriptor BindResult(
        LoomToolDescriptor descriptor,
        LoomToolAIFunctionOptions options)
    {
        return options.BindResult?.Invoke(descriptor) ?? CreateDefaultResultBinding(descriptor, options);
    }

    private static LoomToolParameterBindingDescriptor CreateDefaultParameterBinding(LoomToolParameterDescriptor parameter)
    {
        var kind = parameter.Type switch
        {
            var t when t == typeof(IServiceProvider) => LoomToolParameterBindingKind.ServiceProvider,
            var t when t == typeof(CancellationToken) => LoomToolParameterBindingKind.CancellationToken,
            var t when t == typeof(AIFunctionArguments) => LoomToolParameterBindingKind.Runtime,
            _ => LoomToolParameterBindingKind.AiArgument
        };

        var isVisibleToModel = kind == LoomToolParameterBindingKind.AiArgument;
        return new LoomToolParameterBindingDescriptor(
            parameter,
            kind,
            isVisibleToModel,
            ExcludeFromSchema: !isVisibleToModel);
    }

    private static LoomToolResultBindingDescriptor CreateDefaultResultBinding(
        LoomToolDescriptor descriptor,
        LoomToolAIFunctionOptions options)
    {
        var isVisibleToModel = descriptor.OutputType is not null && !options.ExcludeResultSchema;
        return new LoomToolResultBindingDescriptor(
            descriptor.OutputType,
            isVisibleToModel,
            ExcludeFromSchema: !isVisibleToModel);
    }
}

public enum LoomToolParameterBindingKind
{
    AiArgument,
    ServiceProvider,
    CancellationToken,
    Runtime
}

public sealed record LoomToolParameterBindingDescriptor(
    LoomToolParameterDescriptor Parameter,
    LoomToolParameterBindingKind Kind,
    bool IsVisibleToModel,
    bool ExcludeFromSchema);

public sealed record LoomToolResultBindingDescriptor(
    Type? Type,
    bool IsVisibleToModel,
    bool ExcludeFromSchema);

public sealed record LoomToolBindingSurface(
    IReadOnlyList<LoomToolParameterBindingDescriptor> Parameters,
    LoomToolResultBindingDescriptor Result);

public sealed record LoomToolParameterBindingContext(
    LoomToolDescriptor Tool,
    LoomToolParameterBindingDescriptor Binding,
    IServiceProvider Services,
    AIFunctionArguments Arguments,
    int Index,
    CancellationToken CancellationToken);

public sealed record LoomToolResultBindingContext(
    LoomToolDescriptor Tool,
    LoomToolResultBindingDescriptor Binding,
    object? Result);

public sealed class LoomToolAIFunctionOptions
{
    public static LoomToolAIFunctionOptions Default { get; } = new();

    public Func<LoomToolParameterDescriptor, LoomToolParameterBindingDescriptor>? BindParameter { get; init; }

    public Func<LoomToolParameterBindingContext, object?>? ResolveParameter { get; init; }

    public Func<LoomToolDescriptor, LoomToolResultBindingDescriptor>? BindResult { get; init; }

    public Func<LoomToolResultBindingContext, object?, object?>? MarshalResult { get; init; }

    public bool ExcludeResultSchema { get; init; }

    public JsonSerializerOptions? SerializerOptions { get; init; }

    public IReadOnlyDictionary<string, object?>? AdditionalProperties { get; init; }
}

internal sealed class LoomToolAIFunction : AIFunction
{
    private static readonly JsonSerializerOptions DefaultSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly LoomToolDescriptor _descriptor;
    private readonly LoomToolAIFunctionOptions _options;
    private readonly IServiceProvider? _services;
    private readonly LoomToolBindingSurface _bindingSurface;
    private readonly JsonElement _jsonSchema;
    private readonly JsonElement? _returnJsonSchema;
    private readonly IReadOnlyDictionary<string, object?> _additionalProperties;

    public LoomToolAIFunction(
        LoomToolDescriptor descriptor,
        LoomToolAIFunctionOptions options,
        IServiceProvider? services)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(options);
        _descriptor = descriptor;
        _options = options;
        _services = services;
        _bindingSurface = descriptor.GetBindingSurface(options);
        _jsonSchema = BuildJsonSchema(_bindingSurface);
        _returnJsonSchema = BuildReturnJsonSchema(_bindingSurface.Result, options);
        _additionalProperties = BuildAdditionalProperties(descriptor, _bindingSurface, options);
    }

    public override string Name => _descriptor.Name;

    public override string Description => BuildDescription(_descriptor);

    public override JsonElement JsonSchema => _jsonSchema;

    public override JsonElement? ReturnJsonSchema => _returnJsonSchema;

    public override JsonSerializerOptions JsonSerializerOptions => _options.SerializerOptions ?? DefaultSerializerOptions;

    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _additionalProperties;

    public override object? GetService(Type serviceType, object? serviceKey)
    {
        if (serviceType == typeof(LoomToolDescriptor))
            return _descriptor;

        if (serviceType == typeof(LoomToolAIFunctionOptions))
            return _options;

        if (serviceType == typeof(LoomToolBindingSurface))
            return _bindingSurface;

        if (serviceType == typeof(LoomToolParameterBindingDescriptor[]))
            return _bindingSurface.Parameters.ToArray();

        if (serviceType == typeof(LoomToolResultBindingDescriptor))
            return _bindingSurface.Result;

        return base.GetService(serviceType, serviceKey);
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services ?? _services ?? NullServiceProvider.Instance;
        var parameters = new object?[_bindingSurface.Parameters.Count];

        for (var i = 0; i < _bindingSurface.Parameters.Count; i++)
            parameters[i] = ResolveParameter(_bindingSurface.Parameters[i], arguments, services, i, cancellationToken);

        var result = await _descriptor.Invoker(services, parameters, cancellationToken).ConfigureAwait(false);
        return MarshalResult(result);
    }

    private object? ResolveParameter(
        LoomToolParameterBindingDescriptor binding,
        AIFunctionArguments arguments,
        IServiceProvider services,
        int index,
        CancellationToken cancellationToken)
    {
        if (_options.ResolveParameter is not null)
        {
            var context = new LoomToolParameterBindingContext(
                _descriptor,
                binding,
                services,
                arguments,
                index,
                cancellationToken);

            return _options.ResolveParameter(context);
        }

        return binding.Kind switch
        {
            LoomToolParameterBindingKind.AiArgument => ReadArgument(arguments, binding.Parameter),
            LoomToolParameterBindingKind.ServiceProvider => services,
            LoomToolParameterBindingKind.CancellationToken => cancellationToken,
            LoomToolParameterBindingKind.Runtime => ResolveRuntimeValue(binding, arguments, services, cancellationToken),
            _ => ResolveRuntimeValue(binding, arguments, services, cancellationToken)
        };
    }

    private object? ResolveRuntimeValue(
        LoomToolParameterBindingDescriptor binding,
        AIFunctionArguments arguments,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (binding.Parameter.Type == typeof(IServiceProvider))
            return services;

        if (binding.Parameter.Type == typeof(CancellationToken))
            return cancellationToken;

        if (binding.Parameter.Type == typeof(AIFunctionArguments))
            return arguments;

        var service = services.GetService(binding.Parameter.Type);
        if (service is not null)
            return service;

        return binding.IsVisibleToModel
            ? ReadArgument(arguments, binding.Parameter)
            : ReadArgumentOrDefault(binding.Parameter);
    }

    private object? ReadArgument(AIFunctionArguments arguments, LoomToolParameterDescriptor parameter)
    {
        if (arguments.TryGetValue(parameter.Name, out var value))
            return ConvertArgument(value, parameter.Type);

        return ReadArgumentOrDefault(parameter);
    }

    private object? ReadArgumentOrDefault(LoomToolParameterDescriptor parameter)
    {
        if (parameter.HasDefaultValue && parameter.DefaultValueLiteral is not null)
            return ConvertLiteral(parameter.DefaultValueLiteral, parameter.Type);

        if (parameter.IsNullable || !parameter.Type.IsValueType)
            return null;

        throw new ArgumentException(
            $"Missing required argument '{parameter.Name}' for Loom tool '{Name}'.",
            nameof(parameter));
    }

    private object? ConvertArgument(object? value, Type type)
    {
        if (value is null)
            return null;

        var targetType = Nullable.GetUnderlyingType(type) ?? type;

        if (targetType.IsInstanceOfType(value))
            return value;

        if (targetType == typeof(object))
            return value;

        if (value is JsonElement element)
            return element.Deserialize(targetType, JsonSerializerOptions);

        if (value is JsonDocument document)
            return document.RootElement.Deserialize(targetType, JsonSerializerOptions);

        if (value is JsonNode node)
            return node.Deserialize(targetType, JsonSerializerOptions);

        var payload = JsonSerializer.Serialize(value, value.GetType(), JsonSerializerOptions);
        return JsonSerializer.Deserialize(payload, targetType, JsonSerializerOptions);
    }

    private object? ConvertLiteral(string literal, Type type)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        return JsonSerializer.Deserialize(literal, targetType, JsonSerializerOptions);
    }

    private object? MarshalResult(object? result)
    {
        if (_options.MarshalResult is not null)
        {
            var context = new LoomToolResultBindingContext(_descriptor, _bindingSurface.Result, result);
            return _options.MarshalResult(context, result);
        }

        if (result is null)
            return null;

        if (result is JsonElement or JsonNode or JsonDocument)
            return result;

        if (IsDirectlySerializable(result.GetType()))
            return result;

        var resultType = _bindingSurface.Result.Type ?? result.GetType();
        return JsonSerializer.SerializeToElement(result, resultType, JsonSerializerOptions);
    }

    private static bool IsDirectlySerializable(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
            return true;

        return type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(Guid) ||
               type == typeof(TimeSpan) ||
               type == typeof(Uri);
    }

    private static JsonElement BuildJsonSchema(LoomToolBindingSurface bindingSurface)
    {
        using var document = JsonDocument.Parse(
            LoomJsonSchemaWriter.WriteToolParametersSchema(bindingSurface.Parameters.Where(static parameter => !parameter.ExcludeFromSchema && parameter.IsVisibleToModel).Select(static parameter => parameter.Parameter)));
        return document.RootElement.Clone();
    }

    private static JsonElement? BuildReturnJsonSchema(
        LoomToolResultBindingDescriptor resultBinding,
        LoomToolAIFunctionOptions options)
    {
        if (options.ExcludeResultSchema || !resultBinding.IsVisibleToModel || resultBinding.Type is null)
            return null;

        using var document = JsonDocument.Parse(LoomJsonSchemaWriter.WriteTypeSchema(resultBinding.Type));
        return document.RootElement.Clone();
    }

    private static Dictionary<string, object?> BuildAdditionalProperties(
        LoomToolDescriptor descriptor,
        LoomToolBindingSurface bindingSurface,
        LoomToolAIFunctionOptions options)
    {
        var additionalProperties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["loom.binding.parameters"] = bindingSurface.Parameters.Select(static binding => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = binding.Parameter.Name,
                ["kind"] = binding.Kind.ToString(),
                ["visible"] = binding.IsVisibleToModel,
                ["excludeFromSchema"] = binding.ExcludeFromSchema,
                ["type"] = binding.Parameter.Type.FullName ?? binding.Parameter.Type.Name
            }).ToArray(),
            ["loom.binding.result"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["excludeFromSchema"] = bindingSurface.Result.ExcludeFromSchema,
                ["type"] = bindingSurface.Result.Type?.FullName,
                ["visible"] = bindingSurface.Result.IsVisibleToModel
            },
            ["loom.declaringType"] = descriptor.DeclaringType.FullName ?? descriptor.DeclaringType.Name,
            ["loom.doNotUseWhen"] = descriptor.DoNotUseWhen,
            ["loom.methodName"] = descriptor.MethodName,
            ["loom.outputType"] = descriptor.OutputType?.FullName,
            ["loom.phase"] = descriptor.Phase.ToString(),
            ["loom.requiredCapabilities"] = descriptor.RequiredCapabilities.ToArray(),
            ["loom.requiresApproval"] = descriptor.RequiresApproval,
            ["loom.sideEffect"] = descriptor.SideEffect.ToString(),
            ["loom.useOnlyWhen"] = descriptor.UseOnlyWhen
        };

        if (options.AdditionalProperties is not null)
        {
            foreach (var (key, value) in options.AdditionalProperties)
                additionalProperties[key] = value;
        }

        return additionalProperties;
    }

    private static string BuildDescription(LoomToolDescriptor descriptor)
    {
        var parts = new List<string> { descriptor.Description };

        if (!string.IsNullOrWhiteSpace(descriptor.UseOnlyWhen))
            parts.Add($"Use only when {descriptor.UseOnlyWhen}.");

        if (!string.IsNullOrWhiteSpace(descriptor.DoNotUseWhen))
            parts.Add($"Do not use when {descriptor.DoNotUseWhen}.");

        return string.Join(' ', parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private sealed class NullServiceProvider : IServiceProvider
    {
        public static readonly NullServiceProvider Instance = new();

        private NullServiceProvider()
        {
        }

        public object? GetService(Type serviceType) => null;
    }
}
