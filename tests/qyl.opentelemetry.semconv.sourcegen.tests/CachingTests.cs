using System.Reflection;
using ANcpLua.Roslyn.Utilities.Testing;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Phase 2 caching gate: runs <see cref="SemConvAttributesGenerator"/> twice across
/// independent <see cref="CSharpCompilation"/> instances built from the same source
/// and asserts the user-defined pipeline steps reach a stable cached state on the
/// second run. The forbidden-type analyzer doubles as a static guarantee that no
/// Roslyn runtime types (<c>ISymbol</c>, <c>Compilation</c>, <c>SyntaxNode</c>, etc.)
/// are retained in cached pipeline values — those would defeat caching and bloat
/// IDE memory.
/// </summary>
public sealed class CachingTests
{
    [Fact]
    public void Generator_Has_No_Forbidden_Types_And_Caches_Observable_Steps()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionAttributes("disk")]
            internal static partial class DiskAttributes;
            """;

        var (firstRun, secondRun, report) = RunGeneratorTwice<SemConvAttributesGenerator>(source);

        // Direct output check; GeneratorCachingReport.ProducedOutput collides with
        // SemanticConventionAttributes*.g.cs against the IsInfrastructureFile pattern
        // ("Attributes.g.cs") and would yield false-negatives for this generator's
        // domain output (DiskAttributes.g.cs, HttpAttributes.g.cs, etc.).
        firstRun.GeneratedTrees.Should().NotBeEmpty();
        secondRun.GeneratedTrees.Should().NotBeEmpty();

        report.ObservableSteps.Should().NotBeEmpty("the generator must expose at least one user-defined pipeline step");

        foreach (var step in report.ObservableSteps)
        {
            step.IsCachedSuccessfully.Should().BeTrue(
                $"observable step '{step.StepName}' should reach a cached/unchanged state on the second run ({step.FormatBreakdown()})");
            if (IsUserPipelineStep(step.StepName))
                step.HasForbiddenTypes.Should().BeFalse(
                    $"observable step '{step.StepName}' must not carry forbidden Roslyn types in its cached output");
        }
    }

    [Fact]
    public void Generator_Caches_Across_Multiple_Markers()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionAttributes("disk")]
            internal static partial class DiskAttributes;

            [SemanticConventionAttributes("http")]
            internal static partial class HttpAttributes;

            [SemanticConventionAttributes("network")]
            internal static partial class NetworkAttributes;
            """;

        var (firstRun, secondRun, report) = RunGeneratorTwice<SemConvAttributesGenerator>(source);

        firstRun.GeneratedTrees.Should().NotBeEmpty();
        secondRun.GeneratedTrees.Should().NotBeEmpty();
        report.ObservableSteps.Should().NotBeEmpty();

        foreach (var step in report.ObservableSteps)
        {
            step.IsCachedSuccessfully.Should().BeTrue(
                $"step '{step.StepName}' should cache across multiple markers ({step.FormatBreakdown()})");
            if (IsUserPipelineStep(step.StepName))
                step.HasForbiddenTypes.Should().BeFalse(
                    $"step '{step.StepName}' must not carry forbidden Roslyn types in its cached output");
        }
    }

    [Fact]
    public void MetricsGenerator_Has_No_Forbidden_Types_And_Caches()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionMetrics("http.server")]
            internal static partial class HttpServerMetrics;
            """;

        AssertGeneratorCachesObservableSteps<SemConvMetricsGenerator>(source);
    }

    [Fact]
    public void EventsGenerator_Has_No_Forbidden_Types_And_Caches()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionEvents("session")]
            internal static partial class SessionEvents;
            """;

        AssertGeneratorCachesObservableSteps<SemConvEventsGenerator>(source);
    }

    [Fact]
    public void MetersGenerator_Has_No_Forbidden_Types_And_Caches()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionMeters("http.server")]
            internal static partial class HttpServerMeters;
            """;

        AssertGeneratorCachesObservableSteps<SemConvMetersGenerator>(source);
    }

    [Fact]
    public void ActivitiesGenerator_Has_No_Forbidden_Types_And_Caches()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace MyApp;
            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        AssertGeneratorCachesObservableSteps<SemConvActivitiesGenerator>(source);
    }

    private static void AssertGeneratorCachesObservableSteps<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        var (firstRun, secondRun, report) = RunGeneratorTwice<TGenerator>(source);

        firstRun.GeneratedTrees.Should().NotBeEmpty();
        secondRun.GeneratedTrees.Should().NotBeEmpty();
        report.ObservableSteps.Should().NotBeEmpty(
            $"{typeof(TGenerator).Name} must expose at least one user-defined pipeline step");

        foreach (var step in report.ObservableSteps)
        {
            step.IsCachedSuccessfully.Should().BeTrue(
                $"{typeof(TGenerator).Name} step '{step.StepName}' should cache on the second run ({step.FormatBreakdown()})");
            if (IsUserPipelineStep(step.StepName))
                step.HasForbiddenTypes.Should().BeFalse(
                    $"{typeof(TGenerator).Name} step '{step.StepName}' must not carry forbidden Roslyn types in its cached output");
        }
    }

    // GeneratorCachingReport filters known infrastructure steps from ObservableSteps,
    // but Roslyn's auto-generated internal projection steps under
    // ForAttributeWithMetadataName (named "result_*") slip through the
    // StepClassification pattern list and always carry a Compilation reference
    // through internal pipeline state. They are not user-controllable; only steps
    // the generator author named via WithTrackingName / WhereNotNull / Select
    // are meaningful targets for the forbidden-type gate.
    private static bool IsUserPipelineStep(string stepName) =>
        !stepName.StartsWith("result_", StringComparison.Ordinal);

    private static (GeneratorDriverRunResult FirstRun, GeneratorDriverRunResult SecondRun, GeneratorCachingReport Report)
        RunGeneratorTwice<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetBaseReferences();

        var firstCompilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { generator.AsSourceGenerator() },
            additionalTexts: null,
            parseOptions: null,
            optionsProvider: null,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(firstCompilation, TestContext.Current.CancellationToken);
        var firstRun = driver.GetRunResult();

        // Run #2 against a fresh compilation built from the same syntax tree;
        // forces the driver to re-walk the pipeline and reveals cache state.
        var secondCompilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        driver = driver.RunGenerators(secondCompilation, TestContext.Current.CancellationToken);
        var secondRun = driver.GetRunResult();

        return (firstRun, secondRun, GeneratorCachingReport.Create(firstRun, secondRun, typeof(TGenerator)));
    }

    private static MetadataReference[] GetBaseReferences()
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location)
        ];

        var netstandard = Assembly.Load("netstandard, Version=2.0.0.0");
        references.Add(MetadataReference.CreateFromFile(netstandard.Location));

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var systemRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(systemRuntime))
            references.Add(MetadataReference.CreateFromFile(systemRuntime));

        return [.. references];
    }
}
