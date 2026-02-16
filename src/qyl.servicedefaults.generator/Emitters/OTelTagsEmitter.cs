using System.Collections.Immutable;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Emitters;

/// <summary>
///     Emits Activity.SetTag() extension methods for types with [OTel] attributes.
/// </summary>
internal static class OTelTagsEmitter
{
    /// <summary>
    ///     Emits the extension methods source code for all OTel-tagged members.
    /// </summary>
    public static string Emit(ImmutableArray<OTelTagBinding> tags)
    {
        if (tags.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();

        EmitterHelpers.AppendFileHeader(sb, nullableEnable: true);
        AppendUsings(sb);
        AppendClassOpen(sb);
        AppendExtensionMethods(sb, tags);
        EmitterHelpers.AppendClassClose(sb);

        return sb.ToString();
    }


    private static void AppendUsings(StringBuilder sb)
    {
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine();
    }

    private static void AppendClassOpen(StringBuilder sb) =>
        sb.AppendLine("""
                      namespace Qyl.ServiceDefaults.Generator
                      {
                          /// <summary>
                          /// Generated extension methods for setting OTel tags on Activity.
                          /// </summary>
                          internal static class OTelTagExtensions
                          {
                      """);

    private static void AppendExtensionMethods(
        StringBuilder sb,
        ImmutableArray<OTelTagBinding> tags)
    {
        var byType = tags
            .GroupBy(static t => t.ContainingTypeName)
            .OrderBy(static g => g.Key, StringComparer.Ordinal);

        foreach (var group in byType)
        {
            var typeName = group.Key;
            var typeMembers = group.OrderBy(static t => t.MemberName, StringComparer.Ordinal);

            AppendTypeExtension(sb, typeName, typeMembers);
        }
    }

    private static void AppendTypeExtension(
        StringBuilder sb,
        string typeName,
        IEnumerable<OTelTagBinding> members)
    {
        var methodName = GenerateMethodName(typeName);

        sb.AppendLine($$"""
                                /// <summary>
                                /// Sets OTel tags on the activity from a <see cref="{{typeName}}"/> instance.
                                /// </summary>
                                public static Activity? SetTagsFrom{{methodName}}(this Activity? activity, {{typeName}} value)
                                {
                                    if (activity is null || value is null)
                                        return activity;

                        """);

        foreach (var member in members)
            AppendTagSetter(sb, member);

        sb.AppendLine("""
                                  return activity;
                              }

                      """);
    }

    private static void AppendTagSetter(StringBuilder sb, OTelTagBinding tag)
    {
        var accessor = $"value.{tag.MemberName}";
        var attributeName = tag.AttributeName;

        if (tag.SkipIfNull && (tag.IsNullable || !EmitterHelpers.IsPrimitiveValueType(tag.MemberTypeName)))
        {
            sb.AppendLine($"""
                                           if ({accessor} is not null)
                                               activity.SetTag("{attributeName}", {accessor});
                           """);
        }
        else
        {
            sb.AppendLine($"""
                                           activity.SetTag("{attributeName}", {accessor});
                           """);
        }
    }

    private static string GenerateMethodName(string typeName)
    {
        var name = typeName
            .Replace("global::", "")
            .Replace(".", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(" ", "");

        return name;
    }
}
