
using Microsoft.CodeAnalysis;

namespace Qyl.Instrumentation.Generators;

internal static class GeneratorPipelineHelpers
{
    public const string QylServiceDefaultsTypeName = "Qyl.Instrumentation.QylServiceDefaults";
    public const string WebApplicationBuilderTypeName = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
    public const string WebApplicationTypeName = "Microsoft.AspNetCore.Builder.WebApplication";

    public static bool IsQylRuntimeReferenced(Compilation compilation, CancellationToken _) =>
        compilation.GetTypeByMetadataName(QylServiceDefaultsTypeName) is not null;
}
