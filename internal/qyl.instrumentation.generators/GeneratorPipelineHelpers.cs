// Copyright (c) 2025-2026 ancplua

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Qyl.Instrumentation.Generators;

/// <summary>
///     Cross-generator helpers shared by every <c>[Generator]</c> class in this assembly.
///     Each per-concern generator (builder/DB/tool-manifest/registry) runs its own Roslyn
///     incremental pipeline; these helpers cover the prerequisite checks and the uniform
///     "collect → gate → emit" emitter registration shape all of them use.
/// </summary>
internal static class GeneratorPipelineHelpers
{
    public const string QylServiceDefaultsTypeName = "Qyl.Instrumentation.QylServiceDefaults";
    public const string GeneratedActivitySourceAttributeName = "Qyl.Instrumentation.GeneratedActivitySourceAttribute";
    public const string GeneratedMeterAttributeName = "Qyl.Instrumentation.GeneratedMeterAttribute";
    public const string WebApplicationBuilderTypeName = "Microsoft.AspNetCore.Builder.WebApplicationBuilder";
    public const string WebApplicationTypeName = "Microsoft.AspNetCore.Builder.WebApplication";

    /// <summary>
    ///     True when the consumer assembly references the Qyl.Instrumentation runtime —
    ///     the prerequisite gate for every codegen pipeline that emits code calling into it.
    /// </summary>
    public static bool IsQylRuntimeReferenced(Compilation compilation, CancellationToken _) =>
        compilation.GetTypeByMetadataName(QylServiceDefaultsTypeName) is not null;

    /// <summary>
    ///     Reads an MSBuild property toggle. Returns <c>true</c> when the property is absent
    ///     or set to any value other than the literal string <c>"false"</c> (case-insensitive).
    /// </summary>
    public static bool IsPipelineEnabled(AnalyzerConfigOptionsProvider options, string propertyName) =>
        !options.GlobalOptions.TryGetValue($"build_property.{propertyName}", out var value)
        || !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Registers the canonical "collect values → gate on flag → emit one file" pipeline used
    ///     by every per-concern generator. Empty/disabled inputs produce <see cref="FileWithName.Empty" />.
    /// </summary>
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
