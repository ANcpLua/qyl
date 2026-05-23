namespace Qyl.Instrumentation.Generators.Emitters;

internal static class EmitterHelpers
{
    private const string InterceptsLocationBlock = """
                                                   namespace System.Runtime.CompilerServices
                                                   {
                                                       [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                                                       file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute
                                                       {
                                                           public int Version { get; } = version;
                                                           public string Data { get; } = data;
                                                       }
                                                   }
                                                   """;

    public static void AppendInterceptsLocationAttribute(IndentedStringBuilder sb)
    {
        sb.AppendLineNoIndent(InterceptsLocationBlock);
        sb.AppendLine();
    }

    public static string BuildParameterList(
        string containingType,
        EquatableArray<string> parameterTypes,
        EquatableArray<string> parameterNames,
        bool isStatic = false,
        EquatableArray<string> typeParamNames = default)
    {
        var sb = new StringBuilder();

        if (!isStatic)
            sb.Append($"this global::{containingType} @this");

        for (var i = 0; i < parameterTypes.Length; i++)
        {
            if (sb.Length > 0)
                sb.Append(", ");
            var typeName = parameterTypes[i]
                .ToGlobalTypeName(typeParamNames.IsDefaultOrEmpty ? null : typeParamNames.AsImmutableArray());
            sb.Append($"{typeName} {parameterNames[i]}");
        }

        return sb.ToString();
    }

    public static string BuildArgumentList(EquatableArray<string> parameterNames) =>
        parameterNames.Length is 0 ? string.Empty : string.Join(", ", parameterNames);
}
