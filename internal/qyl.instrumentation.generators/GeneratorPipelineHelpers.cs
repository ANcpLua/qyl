
using Microsoft.CodeAnalysis;

namespace Qyl.Instrumentation.Generators;

internal static class GeneratorPipelineHelpers
{
    public const string QylServiceDefaultsTypeName = "Qyl.Instrumentation.QylServiceDefaults";
    public const string WebApplicationBuilderTypeName = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
    public const string WebApplicationTypeName = "Microsoft.AspNetCore.Builder.WebApplication";
    public const string QylInterceptedAspNetCoreTypeName = "Qyl.OpenTelemetry.AutoInstrumentation.QylInterceptedAspNetCore";

    public static bool IsQylRuntimeReferenced(Compilation compilation, CancellationToken _) =>
        compilation.GetTypeByMetadataName(QylServiceDefaultsTypeName) is not null;

    // When Qyl.OpenTelemetry.AutoInstrumentation is referenced, our Build() interceptor routes the
    // build through its QylInterceptedAspNetCore.Build wrapper (which adds the OTel middleware) so a
    // single interceptor owns the call site. The consumer sets the package's opt-out property so the
    // OTel generator yields Build() to us — otherwise the two interceptors would collide (CS9153).
    public static bool IsOtelAutoInstrumentationReferenced(Compilation compilation, CancellationToken _) =>
        compilation.GetTypeByMetadataName(QylInterceptedAspNetCoreTypeName) is not null;
}
