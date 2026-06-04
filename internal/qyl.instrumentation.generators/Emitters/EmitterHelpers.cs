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
}
