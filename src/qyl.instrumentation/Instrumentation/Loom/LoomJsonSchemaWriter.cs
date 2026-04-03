using System.Text.Json;

namespace Qyl.Instrumentation.Instrumentation.Loom;

public static class LoomJsonSchemaWriter
{
    public static string WriteToolContract(LoomToolDescriptor descriptor)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var parameter in descriptor.Parameters)
        {
            properties[parameter.Name] = CreateSchemaNode(
                parameter.Type,
                parameter.IsNullable,
                parameter.Description,
                parameter.EnumValues);

            if (!parameter.IsNullable && !parameter.HasDefaultValue)
                required.Add(parameter.Name);
        }

        var function = new Dictionary<string, object?>
        {
            ["name"] = descriptor.Name,
            ["description"] = BuildToolDescription(descriptor),
            ["parameters"] = new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
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

    private static object CreateSchemaNode(
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
