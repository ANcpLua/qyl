using System.Text.Json;

namespace Qyl.Instrumentation.Instrumentation.Loom;

public static class LoomJsonSchemaWriter
{
    public static string WriteToolParametersSchema(IEnumerable<LoomToolParameterDescriptor> parameters)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var parameter in parameters)
        {
            properties[parameter.Name] = CreateSchemaNode(
                parameter.Type,
                parameter.IsNullable,
                parameter.Description,
                parameter.EnumValues);

            if (parameter is { IsNullable: false, HasDefaultValue: false })
                required.Add(parameter.Name);
        }

        var root = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        return JsonSerializer.Serialize(root, JsonOptions);
    }

    public static string WriteToolContract(LoomToolDescriptor descriptor)
    {
        var function = new Dictionary<string, object?>
        {
            ["name"] = descriptor.Name,
            ["description"] = BuildToolDescription(descriptor),
            ["parameters"] = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = descriptor.Parameters.ToDictionary(
                    static parameter => parameter.Name,
                    static parameter => CreateSchemaNode(
                        parameter.Type,
                        parameter.IsNullable,
                        parameter.Description,
                        parameter.EnumValues),
                    StringComparer.Ordinal),
                ["required"] = descriptor.Parameters
                    .Where(static parameter => parameter is { IsNullable: false, HasDefaultValue: false })
                    .Select(static parameter => parameter.Name)
                    .ToArray(),
                ["additionalProperties"] = false
            }
        };

        var root = new Dictionary<string, object?>
        {
            ["type"] = "function",
            ["function"] = function
        };

        return JsonSerializer.Serialize(root, JsonOptions);
    }

    public static string WriteContractSchema(LoomContractDescriptor descriptor)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var property in descriptor.Properties)
        {
            properties[property.Name] = CreateSchemaNode(
                property.Type,
                property.IsNullable,
                null,
                property.EnumValues);

            if (property.IsRequired)
                required.Add(property.Name);
        }

        var root = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["title"] = descriptor.Name,
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false
        };

        return JsonSerializer.Serialize(root, JsonOptions);
    }

    public static string WriteTypeSchema(Type type)
    {
        var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
        var actualType = nullableUnderlyingType ?? type;
        var root = new Dictionary<string, object?>
        {
            ["title"] = actualType.Name,
            ["type"] = CreateTypeNode(type)
        };

        if (nullableUnderlyingType is not null)
            root["nullable"] = true;

        return JsonSerializer.Serialize(root, JsonOptions);
    }

    private static Dictionary<string, object?> CreateSchemaNode(
        Type type,
        bool isNullable,
        string? description,
        IReadOnlyList<string> enumValues)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;
        var node = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(description))
            node["description"] = description;

        if (enumValues.Count > 0)
        {
            node["type"] = "string";
            node["enum"] = enumValues;
            return node;
        }

        node["type"] = actualType switch
        {
            var t when t == typeof(string) => "string",
            var t when t == typeof(bool) => "boolean",
            var t when t == typeof(byte) ||
                      t == typeof(sbyte) ||
                      t == typeof(short) ||
                      t == typeof(ushort) ||
                      t == typeof(int) ||
                      t == typeof(uint) ||
                      t == typeof(long) ||
                      t == typeof(ulong) => "integer",
            var t when t == typeof(float) ||
                      t == typeof(double) ||
                      t == typeof(decimal) => "number",
            _ => "object"
        };

        if (isNullable)
            node["nullable"] = true;

        return node;
    }

    private static object CreateTypeNode(Type type)
    {
        var actualType = Nullable.GetUnderlyingType(type) ?? type;

        if (actualType.IsEnum)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "string",
                ["enum"] = Enum.GetNames(actualType)
            };
        }

        if (actualType == typeof(string))
            return "string";

        if (actualType == typeof(bool))
            return "boolean";

        if (actualType == typeof(byte) ||
            actualType == typeof(sbyte) ||
            actualType == typeof(short) ||
            actualType == typeof(ushort) ||
            actualType == typeof(int) ||
            actualType == typeof(uint) ||
            actualType == typeof(long) ||
            actualType == typeof(ulong))
            return "integer";

        if (actualType == typeof(float) ||
            actualType == typeof(double) ||
            actualType == typeof(decimal))
            return "number";

        if (actualType == typeof(Guid) ||
            actualType == typeof(DateTime) ||
            actualType == typeof(DateTimeOffset) ||
            actualType == typeof(TimeSpan) ||
            actualType == typeof(Uri))
            return "string";

        if (actualType.IsArray)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["items"] = CreateTypeNode(actualType.GetElementType() ?? typeof(object))
            };
        }

        if (TryGetEnumerableElementType(actualType, out var elementType))
        {
            return new Dictionary<string, object?>
            {
                ["type"] = "array",
                ["items"] = CreateTypeNode(elementType)
            };
        }

        return "object";
    }

    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type == typeof(string))
        {
            elementType = typeof(char);
            return false;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            elementType = type.GetGenericArguments()[0];
            return true;
        }

        var enumerable = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (enumerable is not null)
        {
            elementType = enumerable.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }

    private static string BuildToolDescription(LoomToolDescriptor descriptor)
    {
        var parts = new List<string> { descriptor.Description };

        if (!string.IsNullOrWhiteSpace(descriptor.UseOnlyWhen))
            parts.Add($"Use only when {descriptor.UseOnlyWhen}.");

        if (!string.IsNullOrWhiteSpace(descriptor.DoNotUseWhen))
            parts.Add($"Do not use when {descriptor.DoNotUseWhen}.");

        return string.Join(' ', parts.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
