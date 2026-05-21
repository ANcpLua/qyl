using System.IO;
using System.Reflection;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Extractors;

/// <summary>
/// Loads the attribute-shaped projection of the embedded resolved registry for
/// PR-E (<c>System.Diagnostics.Activity</c> typed setters). Returns the catalog
/// flat — the activity emitter filters by marker prefix.
/// </summary>
internal static class ActivityRegistryLoader
{
    private const string ResourceName = "Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.resolved-registry.json";

    private static readonly Lazy<ActivityRegistryModel> _registry = new(LoadFromEmbeddedResource);

    public static ActivityRegistryModel Registry => _registry.Value;

    private static ActivityRegistryModel LoadFromEmbeddedResource()
    {
        var assembly = typeof(ActivityRegistryLoader).GetTypeInfo().Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"Embedded resource '{ResourceName}' not found in {assembly.FullName}.");

        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        var root = JsonReader.Parse(text);
        return ParseRegistry(root);
    }

    internal static ActivityRegistryModel ParseRegistry(JsonValue root)
    {
        if (root is not JsonObject obj)
            return new ActivityRegistryModel(default);

        if (obj.TryGetArray("catalog") is not { } catalogArr)
            return new ActivityRegistryModel(default);

        var attributes = new List<ActivityAttributeModel>();
        foreach (var item in catalogArr.Items)
        {
            if (item is not JsonObject attr) continue;

            var key = attr.GetString("key");
            var typeValue = attr.TryGet("type");
            var (parameterType, isTemplate, isEnum, enumMembers) = ResolveSetterShape(typeValue);

            attributes.Add(new ActivityAttributeModel(
                Key: key,
                CSharpParameterType: parameterType,
                IsTemplate: isTemplate,
                IsEnum: isEnum,
                EnumMembers: enumMembers,
                Brief: attr.GetString("brief"),
                Stability: ParseStability(attr.GetString("stability")),
                Deprecated: RegistryParsing.ParseDeprecated(attr.TryGet("deprecated") as JsonObject)));
        }

        return new ActivityRegistryModel(attributes.ToEquatableArray());
    }

    private static (string ParameterType, bool IsTemplate, bool IsEnum, EquatableArray<EnumMemberModel> Members) ResolveSetterShape(JsonValue? typeValue)
    {
        if (typeValue is JsonString s)
        {
            if (s.Value.StartsWith("template[", StringComparison.Ordinal))
            {
                var inner = ExtractTemplateInner(s.Value);
                return (MapPrimitive(inner), true, false, default);
            }
            return (MapPrimitive(s.Value), false, false, default);
        }

        if (typeValue is JsonObject obj && obj.TryGetArray("members") is { } membersArr)
        {
            var members = new List<EnumMemberModel>();
            foreach (var item in membersArr.Items)
            {
                if (item is not JsonObject member) continue;
                members.Add(new EnumMemberModel(
                    Id: member.GetString("id"),
                    Value: member.GetString("value"),
                    Brief: member.GetString("brief"),
                    Stability: ParseStability(member.GetString("stability")),
                    Deprecated: RegistryParsing.ParseDeprecated(member.TryGet("deprecated") as JsonObject)));
            }
            return ("string", false, true, members.ToEquatableArray());
        }

        return ("string", false, false, default);
    }

    private static string MapPrimitive(string primitive) => primitive switch
    {
        "string" => "string",
        "int" => "long",
        "double" => "double",
        "boolean" => "bool",
        "string[]" => "string[]",
        "int[]" => "long[]",
        "double[]" => "double[]",
        "boolean[]" => "bool[]",
        _ => "string"
    };

    private static string ExtractTemplateInner(string raw)
    {
        // raw is e.g. "template[string]" or "template[string[]]"
        const string prefix = "template[";
        if (!raw.StartsWith(prefix, StringComparison.Ordinal) || !raw.EndsWith("]", StringComparison.Ordinal))
            return "string";
        return raw.Substring(prefix.Length, raw.Length - prefix.Length - 1);
    }

    private static StabilityModel ParseStability(string value) => value switch
    {
        "stable" => StabilityModel.Stable,
        "development" => StabilityModel.Development,
        "deprecated" => StabilityModel.Deprecated,
        "alpha" => StabilityModel.Alpha,
        "beta" => StabilityModel.Beta,
        "release_candidate" => StabilityModel.ReleaseCandidate,
        _ => StabilityModel.Development
    };
}
