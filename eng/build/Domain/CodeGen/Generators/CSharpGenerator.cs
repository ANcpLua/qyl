using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Context;

#pragma warning disable CA1305, CA1863 // Build-time code generators use invariant formatting

namespace Domain.CodeGen.Generators;

/// <summary>
///     Generates C# code from schema definitions.
///     Produces:
///     - Primitive wrapper structs (SessionId, UnixNano, TraceId, SpanId)
///     - Model records (SpanRecord, GenAiSpanData, SessionSummary, TraceNode)
///     - Enum types (SpanKind, StatusCode)
///     - GenAiAttributes constants class
/// </summary>
public sealed class CSharpGenerator : IGenerator
{
    const string GeneratorName = nameof(CSharpGenerator);

    public string Name => "CSharp";

    public FrozenDictionary<string, string> Generate(QylSchema schema, BuildPaths paths, string rootNamespace)
    {
        var outputs = new Dictionary<string, string>();

        // Generate primitives
        foreach (var primitive in schema.Primitives)
        {
            var relativePath = $"Primitives/{primitive.Name}.g.cs";
            var content = EmitPrimitive(primitive, rootNamespace, relativePath);
            outputs[relativePath] = content;
        }

        // Generate models
        foreach (var model in schema.Models)
        {
            var relativePath = $"Models/{model.Name}.g.cs";
            var content = EmitModel(model, rootNamespace, relativePath);
            outputs[relativePath] = content;
        }

        // Generate GenAiAttributes constants
        const string attrsPath = "Attributes/GenAiAttributes.g.cs";
        var attrsContent = EmitGenAiAttributes(schema.GenAiAttributes, rootNamespace, attrsPath);
        outputs[attrsPath] = attrsContent;

        return outputs.ToFrozenDictionary();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Primitive Emission
    // ════════════════════════════════════════════════════════════════════════

    static string EmitPrimitive(PrimitiveDefinition primitive, string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using System.Globalization;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Primitives;");
        sb.AppendLine();

        // XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {primitive.Description}");
        sb.AppendLine("/// </summary>");

        // JSON converter attribute
        if (primitive.JsonConverter is not null)
            sb.AppendLine($"[JsonConverter(typeof({primitive.JsonConverter}))]");

        // Struct declaration with interfaces
        var interfaces = string.Join(", ", primitive.Implements.Concat(
        [
            $"IEquatable<{primitive.Name}>",
            $"IComparable<{primitive.Name}>",
            "IFormattable"
        ]));

        sb.AppendLine($"public readonly partial struct {primitive.Name} : {interfaces}");
        sb.AppendLine("{");

        // Backing field
        sb.AppendLine($"    private readonly {primitive.UnderlyingType} _value;");
        sb.AppendLine();

        // Constructor
        sb.AppendLine(
            $"    /// <summary>Creates a new <see cref=\"{primitive.Name}\"/> from the underlying value.</summary>");
        sb.AppendLine($"    public {primitive.Name}({primitive.UnderlyingType} value) => _value = value;");
        sb.AppendLine();

        // Value property
        sb.AppendLine($"    /// <summary>Gets the underlying {primitive.UnderlyingType} value.</summary>");
        sb.AppendLine($"    public {primitive.UnderlyingType} Value => _value;");
        sb.AppendLine();

        // Empty/Default
        sb.AppendLine("    /// <summary>Gets the default (empty) value.</summary>");
        sb.AppendLine($"    public static {primitive.Name} Empty => new({primitive.DefaultValue});");
        sb.AppendLine();

        // Implicit conversions
        sb.AppendLine("    /// <summary>Implicitly converts to the underlying type.</summary>");
        sb.AppendLine(
            $"    public static implicit operator {primitive.UnderlyingType}({primitive.Name} value) => value._value;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Explicitly converts from the underlying type.</summary>");
        sb.AppendLine(
            $"    public static explicit operator {primitive.Name}({primitive.UnderlyingType} value) => new(value);");
        sb.AppendLine();

        // IEquatable
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine($"    public bool Equals({primitive.Name} other) => _value.Equals(other._value);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine(
            $"    public override bool Equals([NotNullWhen(true)] object? obj) => obj is {primitive.Name} other && Equals(other);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine("    public override int GetHashCode() => _value.GetHashCode();");
        sb.AppendLine();

        // IComparable
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine($"    public int CompareTo({primitive.Name} other) => _value.CompareTo(other._value);");
        sb.AppendLine();

        // Operators
        sb.AppendLine("    /// <summary>Equality operator.</summary>");
        sb.AppendLine(
            $"    public static bool operator ==({primitive.Name} left, {primitive.Name} right) => left.Equals(right);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Inequality operator.</summary>");
        sb.AppendLine(
            $"    public static bool operator !=({primitive.Name} left, {primitive.Name} right) => !left.Equals(right);");
        sb.AppendLine();

        // ToString
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine($"    public override string ToString() => _value.{primitive.FormatMethod};");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine(
            "    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);");
        sb.AppendLine();

        // ISpanParsable
        if (primitive.Implements.Contains("ISpanParsable<" + primitive.Name + ">"))
        {
            sb.AppendLine(
                $"    /// <summary>Parses a span of characters into a <see cref=\"{primitive.Name}\"/>.</summary>");
            sb.AppendLine(
                $"    public static {primitive.Name} Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)");
            sb.AppendLine($"        => new({primitive.ParseMethod}(s, NumberStyles.None, provider));");
            sb.AppendLine();
            sb.AppendLine(
                $"    /// <summary>Tries to parse a span of characters into a <see cref=\"{primitive.Name}\"/>.</summary>");
            sb.AppendLine(
                $"    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out {primitive.Name} result)");
            sb.AppendLine("    {");
            sb.AppendLine(
                $"        if ({primitive.UnderlyingType}.TryParse(s, NumberStyles.None, provider, out var value))");
            sb.AppendLine("        {");
            sb.AppendLine($"            result = new {primitive.Name}(value);");
            sb.AppendLine("            return true;");
            sb.AppendLine("        }");
            sb.AppendLine("        result = Empty;");
            sb.AppendLine("        return false;");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // IParsable (string overloads)
        sb.AppendLine($"    /// <summary>Parses a string into a <see cref=\"{primitive.Name}\"/>.</summary>");
        sb.AppendLine($"    public static {primitive.Name} Parse(string s, IFormatProvider? provider = null)");
        sb.AppendLine("        => Parse(s.AsSpan(), provider);");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Tries to parse a string into a <see cref=\"{primitive.Name}\"/>.</summary>");
        sb.AppendLine(
            $"    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out {primitive.Name} result)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (s is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            result = Empty;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine("        return TryParse(s.AsSpan(), provider, out result);");
        sb.AppendLine("    }");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Model Emission
    // ════════════════════════════════════════════════════════════════════════

    static string EmitModel(ModelDefinition model, string rootNamespace, string outputPath)
    {
        if (model.IsEnum)
            return EmitEnum(model, rootNamespace, outputPath);

        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine($"using {rootNamespace}.Primitives;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Models;");
        sb.AppendLine();

        // XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {model.Description}");
        sb.AppendLine("/// </summary>");

        if (model.IsRecord)
        {
            sb.AppendLine($"public sealed partial record {model.Name}");
            sb.AppendLine("{");

            foreach (var prop in model.Properties ?? [])
            {
                sb.AppendLine($"    /// <summary>{prop.Description}</summary>");

                // JSON property name
                var jsonName = ToJsonPropertyName(prop.Name);
                sb.AppendLine($"    [JsonPropertyName(\"{jsonName}\")]");

                // Nullable annotation
                if (!prop.IsRequired && !prop.Type.EndsWith('?'))
                    sb.AppendLine($"    public {prop.Type}? {prop.Name} {{ get; init; }}");
                else if (prop.DefaultValue is not null)
                    sb.AppendLine($"    public {prop.Type} {prop.Name} {{ get; init; }} = {prop.DefaultValue};");
                else
                    sb.AppendLine($"    public required {prop.Type} {prop.Name} {{ get; init; }}");
                sb.AppendLine();
            }

            sb.AppendLine("}");
        }
        else
        {
            // Regular class (not typically used, but supported)
            sb.AppendLine($"public sealed partial class {model.Name}");
            sb.AppendLine("{");

            foreach (var prop in model.Properties ?? [])
            {
                sb.AppendLine($"    /// <summary>{prop.Description}</summary>");
                sb.AppendLine($"    public {prop.Type} {prop.Name} {{ get; set; }}");
                sb.AppendLine();
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    static string EmitEnum(ModelDefinition model, string rootNamespace, string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Models;");
        sb.AppendLine();

        // XML documentation
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {model.Description}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter<" + model.Name + ">))]");
        sb.AppendLine($"public enum {model.Name}");
        sb.AppendLine("{");

        foreach (var value in model.EnumValues ?? [])
        {
            sb.AppendLine($"    /// <summary>{value.Description}</summary>");
            sb.AppendLine($"    {value.Name} = {value.Value},");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GenAiAttributes Constants
    // ════════════════════════════════════════════════════════════════════════

    static string EmitGenAiAttributes(
        IReadOnlyDictionary<string, GenAiAttributeDefinition> attributes,
        string rootNamespace,
        string outputPath)
    {
        var sb = new StringBuilder();

        sb.Append(GeneratedFileHeaders.CSharp(GeneratorName, outputPath));
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}.Attributes;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// OpenTelemetry gen_ai.* semantic convention attribute keys (v1.38).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static partial class GenAiAttributes");
        sb.AppendLine("{");

        // Group by prefix
        var currentGroup = "";
        foreach (var (key, attr) in attributes.OrderBy(a => a.Key))
        {
            var prefix = key.Split('.')[0] + "." + key.Split('.')[1];
            if (prefix != currentGroup)
            {
                currentGroup = prefix;
                sb.AppendLine();
                sb.AppendLine($"    #region {prefix}.*");
                sb.AppendLine();
            }

            // Constant name
            var constName = KeyToConstantName(key);

            // Documentation
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {attr.Description}");
            if (attr.AllowedValues is { Length: > 0 })
                sb.AppendLine($"    /// <para>Allowed values: {string.Join(", ", attr.AllowedValues)}</para>");
            if (attr is { IsDeprecated: true, ReplacedBy: { } replacedBy })
                sb.AppendLine(
                    $"    /// <para>DEPRECATED: Use <see cref=\"{KeyToConstantName(replacedBy)}\"/> instead.</para>");
            sb.AppendLine("    /// </summary>");

            if (attr is { IsDeprecated: true, ReplacedBy: { } replacement })
                sb.AppendLine($"    [Obsolete(\"Use {replacement} instead\")]");

            sb.AppendLine($"    public const string {constName} = \"{key}\";");
            sb.AppendLine();
        }

        sb.AppendLine("    #endregion");
        sb.AppendLine();

        // FrozenSet of all keys for validation
        sb.AppendLine("    /// <summary>All valid gen_ai.* attribute keys.</summary>");
        sb.AppendLine("    public static FrozenSet<string> AllKeys { get; } = new HashSet<string>");
        sb.AppendLine("    {");
        foreach (var key in attributes.Keys.Where(k => !attributes[k].IsDeprecated).OrderBy(k => k))
            sb.AppendLine($"        \"{key}\",");
        sb.AppendLine("    }.ToFrozenSet();");
        sb.AppendLine();

        // Migration dictionary
        sb.AppendLine("    /// <summary>Mapping from deprecated to current attribute keys.</summary>");
        sb.AppendLine(
            "    public static FrozenDictionary<string, string> Migrations { get; } = new Dictionary<string, string>");
        sb.AppendLine("    {");
        foreach (var (key, attr) in attributes.Where(a => a.Value.IsDeprecated && a.Value.ReplacedBy is not null))
            sb.AppendLine($"        [\"{key}\"] = \"{attr.ReplacedBy}\",");
        sb.AppendLine("    }.ToFrozenDictionary();");
        sb.AppendLine();

        // Helper method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Normalizes a potentially deprecated attribute key to its current equivalent.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string Normalize(string key)");
        sb.AppendLine("        => Migrations.TryGetValue(key, out var replacement) ? replacement : key;");

        sb.AppendLine("}");

        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════

    static string ToJsonPropertyName(string propertyName)
    {
        // camelCase conversion
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }

    static string KeyToConstantName(string key)
    {
        // gen_ai.request.model -> GenAiRequestModel
        var parts = key.Split('.');
        var sb = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length is 0) continue;

            var words = part.Split('_');
            foreach (var word in words)
            {
                if (word.Length is 0) continue;
                sb.Append(char.ToUpperInvariant(word[0]));
                sb.Append(word[1..]);
            }
        }

        return sb.ToString();
    }
}