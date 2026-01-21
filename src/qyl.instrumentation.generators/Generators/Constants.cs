namespace qyl.instrumentation.generators.Generators;

internal static class MetadataNames
{
    public const string ServiceDefaultsClass = "qyl.instrumentation.ServiceDefaultsExtensions";
    public const string WebApplicationBuilder = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
}

internal static class MethodNames
{
    public const string Build = "Build";
    public const string AddServiceDefaults = "AddServiceDefaults";
}

internal static class OutputFileNames
{
    public const string Interceptors = "ServiceDefaultsInterceptors.g.cs";
}

internal static class TrackingNames
{
    public const string ServiceDefaultsAvailable = "ServiceDefaultsAvailable";
    public const string InterceptionCandidates = "InterceptionCandidates";
    public const string CollectedBuildCalls = "CollectedBuildCalls";
}
