using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;

namespace Qyl.Instrumentation.Instrumentation.Loom;

public static partial class LoomGeneratedRegistry
{
    public static IReadOnlyList<AIFunction> ToAIFunctions(IServiceProvider? services = null)
        => Tools.ToAIFunctions(services);

    public static IReadOnlyList<AITool> ToToolCatalog(IServiceProvider? services = null)
        => Tools.ToToolCatalog(services);
}

public static class LoomToolDescriptorExtensions
{
    public static AIFunction ToAIFunction(
        this LoomToolDescriptor descriptor,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new LoomToolAIFunction(descriptor, services);
    }

    public static IReadOnlyList<AIFunction> ToAIFunctions(
        this IEnumerable<LoomToolDescriptor> descriptors,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        return descriptors.Select(descriptor => descriptor.ToAIFunction(services)).ToArray();
    }

    public static IReadOnlyList<AITool> ToToolCatalog(
        this IEnumerable<LoomToolDescriptor> descriptors,
        IServiceProvider? services = null)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        return descriptors.Select(descriptor => (AITool)descriptor.ToAIFunction(services)).ToArray();
    }
}

internal sealed class LoomToolAIFunction : AIFunction
{
    private readonly LoomToolDescriptor _descriptor;
    private readonly IServiceProvider? _services;
    private readonly JsonElement _jsonSchema;
    private readonly IReadOnlyDictionary<string, object?> _additionalProperties;

    public LoomToolAIFunction(LoomToolDescriptor descriptor, IServiceProvider? services)
    {
        _descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        _services = services;
        _jsonSchema = BuildJsonSchema(descriptor);
        _additionalProperties = BuildAdditionalProperties(descriptor);
    }

    public override string Name => _descriptor.Name;

    public override string Description => BuildDescription(_descriptor);

    public override JsonElement JsonSchema => _jsonSchema;

    public override JsonElement? ReturnJsonSchema => null;

    public override IReadOnlyDictionary<string, object?>? AdditionalProperties => _additionalProperties;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services ?? _services ?? NullServiceProvider.Instance;
        var parameters = new object?[_descriptor.Parameters.Count];

        for (var i = 0; i < _descriptor.Parameters.Count; i++)
            parameters[i] = GetParameterValue(arguments, _descriptor.Parameters[i]);

        return await _descriptor.Invoker(services, parameters, cancellationToken).ConfigureAwait(false);
    }

    private object? GetParameterValue(
        AIFunctionArguments arguments,
        LoomToolParameterDescriptor parameter)
    {
        if (arguments.TryGetValue(parameter.Name, out var value))
            return ConvertArgument(value, parameter.Type);

        if (parameter.HasDefaultValue && parameter.DefaultValueLiteral is not null)
            return ConvertLiteral(parameter.DefaultValueLiteral, parameter.Type);

        if (parameter.IsNullable || !parameter.Type.IsValueType)
            return null;

        throw new ArgumentException(
            $"Missing required argument '{parameter.Name}' for Loom tool '{Name}'.",
            nameof(arguments));
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

    private static JsonElement BuildJsonSchema(LoomToolDescriptor descriptor)
    {
        using var document = JsonDocument.Parse(LoomJsonSchemaWriter.WriteToolContract(descriptor));
        return document.RootElement.GetProperty("function").GetProperty("parameters").Clone();
    }

    private static IReadOnlyDictionary<string, object?> BuildAdditionalProperties(LoomToolDescriptor descriptor)
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
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
