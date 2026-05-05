
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Qyl.Instrumentation.Generators;

internal static class GeneratorPipelineHelpers
{
    public const string QylServiceDefaultsTypeName = "Qyl.Instrumentation.QylServiceDefaults";
    public const string GeneratedActivitySourceAttributeName = "Qyl.Instrumentation.GeneratedActivitySourceAttribute";
    public const string GeneratedMeterAttributeName = "Qyl.Instrumentation.GeneratedMeterAttribute";
    public const string WebApplicationBuilderTypeName = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
    public const string WebApplicationTypeName = "Microsoft.AspNetCore.Builder.WebApplication";

    public static bool IsQylRuntimeReferenced(Compilation compilation, CancellationToken _) =>
        compilation.GetTypeByMetadataName(QylServiceDefaultsTypeName) is not null;

    public static bool IsPipelineEnabled(AnalyzerConfigOptionsProvider options, string propertyName) =>
        !options.GlobalOptions.TryGetValue($"build_property.{propertyName}", out var value)
        || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    public static void RegisterCollectedEmitterPipeline<T>(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<T> values,
        IncrementalValueProvider<bool> enabledFlag,
        Func<ImmutableArray<T>, string> emitter,
        string generatedFileName,
        string diagnosticId)
        where T : class, IEquatable<T> =>
        values
            .CollectAsEquatableArray()
            .Combine(enabledFlag)
            .SelectAndReportExceptions((input, _) =>
            {
                if (!input.Right || input.Left.IsDefaultOrEmpty) return FileWithName.Empty;
                var sourceCode = emitter(input.Left.AsImmutableArray());
                return string.IsNullOrEmpty(sourceCode)
                    ? FileWithName.Empty
                    : new FileWithName(generatedFileName, sourceCode);
            }, context, diagnosticId)
            .AddSource(context);
}
