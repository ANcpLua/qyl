namespace Qyl.Instrumentation.Generators.Emitters;

/// <summary>
///     Shared helper methods for source emitters.
/// </summary>
internal static class EmitterHelpers
{
    private const string InterceptsLocationBlock = """
                                                   namespace System.Runtime.CompilerServices
                                                   {
                                                       [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]
                                                       file sealed class InterceptsLocationAttribute(int version, string data) : global::System.Attribute;
                                                   }
                                                   """;

    /// <summary>
    ///     Appends the file-scoped InterceptsLocationAttribute required for C# interceptors.
    ///     This attribute is defined as file-scoped to avoid conflicts with other generators.
    /// </summary>
    public static void AppendInterceptsLocationAttribute(IndentedStringBuilder sb)
    {
        sb.AppendLineNoIndent(InterceptsLocationBlock);
        sb.AppendLine();
    }

    /// <summary>
    ///     Builds the parameter list for an interceptor method signature.
    /// </summary>
    /// <param name="containingType">The fully-qualified type name of the intercepted method's class.</param>
    /// <param name="parameterTypes">The parameter type names.</param>
    /// <param name="parameterNames">The parameter names (used in the signature).</param>
    /// <param name="isStatic">If true, omits the <c>this</c> parameter.</param>
    /// <param name="typeParamNames">Optional type parameter names for global type name resolution.</param>
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

    /// <summary>
    ///     Builds the argument list for forwarding to the original method.
    /// </summary>
    public static string BuildArgumentList(EquatableArray<string> parameterNames) =>
        parameterNames.Length is 0 ? string.Empty : string.Join(", ", parameterNames);
}
