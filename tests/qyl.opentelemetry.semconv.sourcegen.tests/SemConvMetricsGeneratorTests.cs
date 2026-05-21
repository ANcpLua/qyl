using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

public sealed class SemConvMetricsGeneratorTests
{
    [Fact]
    public void Emits_Marker_Attribute_PostInitialization()
    {
        const string source = """
            namespace Empty;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricsGenerator>(source);

        var attributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionMetricsAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        attributeFile.Should()
            .Contain("namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;")
            .And.Contain("internal sealed class SemanticConventionMetricsAttribute")
            .And.Contain("public SemanticConventionMetricsAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }

    [Fact]
    public void Emits_HttpServerMetrics_For_HttpServer_Marker()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionMetrics("http.server")]
            internal static partial class HttpServerMetrics;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpServerMetrics.g.cs", StringComparison.Ordinal))
            .ToString();

        // Stable marker: only http.server.request.duration is stable in v1.41.0;
        // the other three http.server.* metrics are development-stability and
        // must be filtered out under StableOnly.
        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class HttpServerMetrics")
            .And.Contain("public const string MetricHttpServerRequestDuration = \"http.server.request.duration\";")
            .And.Contain("public static partial class HttpServerRequestDurationDescriptor")
            .And.Contain("public const string Name = \"http.server.request.duration\";")
            .And.Contain("public const string Unit = \"s\";")
            .And.Contain("public const string Instrument = \"histogram\";")
            .And.Contain("public const string RequirementLevel = \"recommended\";")
            .And.Contain("public const string Brief = \"Duration of HTTP server requests.\";");

        generated.Should()
            .NotContain("http.server.active_requests",
                "active_requests is development-stability and must not appear under [SemanticConventionMetrics]")
            .And.NotContain("http.server.request.body.size",
                "request.body.size is development-stability and must not appear under [SemanticConventionMetrics]")
            .And.NotContain("http.server.response.body.size",
                "response.body.size is development-stability and must not appear under [SemanticConventionMetrics]");
    }

    [Fact]
    public void Emits_All_HttpServerMetrics_For_Incubating_Marker()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMetrics("http.server")]
            internal static partial class HttpServerIncubatingMetrics;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpServerIncubatingMetrics.g.cs", StringComparison.Ordinal))
            .ToString();

        // Incubating marker: all four v1.41.0 http.server.* metrics must appear,
        // covering the stable row plus three development-stability rows.
        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class HttpServerIncubatingMetrics")
            .And.Contain("public const string MetricHttpServerRequestDuration = \"http.server.request.duration\";")
            .And.Contain("public const string MetricHttpServerActiveRequests = \"http.server.active_requests\";")
            .And.Contain("public const string MetricHttpServerRequestBodySize = \"http.server.request.body.size\";")
            .And.Contain("public const string MetricHttpServerResponseBodySize = \"http.server.response.body.size\";")
            .And.Contain("public static partial class HttpServerActiveRequestsDescriptor")
            .And.Contain("public const string Instrument = \"updowncounter\";");
    }

    [Fact]
    public void Descriptor_Preserves_Requirement_Notes_And_Entity_Associations()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMetrics("process.cpu")]
            internal static partial class ProcessCpuMetrics;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("ProcessCpuMetrics.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public static partial class ProcessCpuTimeDescriptor")
            .And.Contain("public const string AttributeCpuMode = \"cpu.mode\";")
            .And.Contain("public const string AttributeCpuModeRequirementLevel = \"required\";")
            .And.Contain("public const string AttributeCpuModeNote")
            .And.Contain("public const int AttributeCpuModeExampleCount = ")
            .And.Contain("public const string AttributeCpuModeExample1")
            .And.Contain("EntityAssociationCount = 1;")
            .And.Contain("public const string EntityAssociationProcess = \"process\";");
    }

    [Fact]
    public void Stable_Marker_Emits_Empty_When_All_Rows_Are_Development()
    {
        // gen_ai.client.* metrics in v1.41.0 are all development-stability;
        // the stable surface must therefore emit a class with no constants/descriptors.
        // The incubating surface for the same prefix should emit the development rows.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionMetrics("gen_ai.client")]
            internal static partial class GenAiClientStableMetrics;

            [SemanticConventionIncubatingMetrics("gen_ai.client")]
            internal static partial class GenAiClientIncubatingMetrics;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricsGenerator>(source);

        var stable = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("GenAiClientStableMetrics.g.cs", StringComparison.Ordinal))
            .ToString();
        var incubating = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("GenAiClientIncubatingMetrics.g.cs", StringComparison.Ordinal))
            .ToString();

        stable.Should()
            .Contain("static partial class GenAiClientStableMetrics")
            .And.NotContain("public const string Metric",
                "no gen_ai.client.* metric is stable in v1.41.0 — stable surface must emit no Metric* constants");

        incubating.Should()
            .Contain("public const string Metric",
                "incubating surface must emit the development-stability gen_ai.client.* rows");
    }

    [Fact]
    public void Compilation_With_Generator_Has_No_Errors()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionMetrics("http.server")]
            internal static partial class HttpServerMetrics;

            [SemanticConventionIncubatingMetrics("http.server")]
            internal static partial class HttpServerIncubatingMetrics;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetricsGenerator>(source);

        var errors = result.OutputCompilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty();
    }
}
