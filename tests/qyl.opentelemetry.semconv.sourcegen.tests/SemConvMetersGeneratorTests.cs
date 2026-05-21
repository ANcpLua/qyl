using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;


/// <summary>
/// PR-D tests: <c>[SemanticConventionMeters(&quot;&lt;prefix&gt;&quot;)]</c> emits
/// typed <c>Meter.Create&lt;Instrument&gt;</c> extension factories.
/// </summary>
public sealed class SemConvMetersGeneratorTests
{
    [Fact]
    public void Emits_MetersMarker_Attribute_PostInitialization()
    {
        const string source = "namespace Empty;";

        var result = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(source);

        var attributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionMetersAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        attributeFile.Should()
            .Contain("namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;")
            .And.Contain("internal sealed class SemanticConventionMetersAttribute")
            .And.Contain("public SemanticConventionMetersAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }

    [Fact]
    public void Emits_Histogram_Factory_For_HttpServer_Marker()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionMeters("http.server")]
            internal static partial class HttpServerMeters;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpServerMeters.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class HttpServerMeters")
            .And.Contain("public static global::System.Diagnostics.Metrics.Histogram<double> CreateHttpServerRequestDurationHistogram(")
            .And.Contain("this global::System.Diagnostics.Metrics.Meter meter)")
            .And.Contain("=> meter.CreateHistogram<double>(")
            .And.Contain("name: \"http.server.request.duration\"")
            .And.Contain("unit: \"s\"")
            .And.Contain("description: \"Duration of HTTP server requests.\"");
    }

    [Fact]
    public void Emits_UpDownCounter_For_HttpServer_ActiveRequests()
    {
        // http.server.active_requests is stability=development — only the
        // incubating marker surfaces it (Phase B-2 stability filter).
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMeters("http.server")]
            internal static partial class HttpServerMeters;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpServerMeters.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public static global::System.Diagnostics.Metrics.UpDownCounter<long> CreateHttpServerActiveRequestsUpdowncounter(")
            .And.Contain("=> meter.CreateUpDownCounter<long>(")
            .And.Contain("name: \"http.server.active_requests\"")
            .And.Contain("unit: \"{request}\"");
    }

    [Fact]
    public void Emits_ObservableGauge_With_ObserveCallback_For_SystemCpu()
    {
        // system.cpu.utilization is stability=development — only the
        // incubating marker surfaces it (Phase B-2 stability filter).
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMeters("system.cpu")]
            internal static partial class SystemCpuMeters;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SystemCpuMeters.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public static global::System.Diagnostics.Metrics.ObservableGauge<double>")
            .And.Contain("global::System.Func<double> observeValue)")
            .And.Contain("=> meter.CreateObservableGauge<double>(")
            .And.Contain("name: \"system.cpu.utilization\"")
            .And.Contain("observeValue: observeValue");
    }

    [Fact]
    public void Histogram_For_Byte_Unit_Selects_Long_ValueType()
    {
        // The By-unit metric under http.client is http.client.request.body.size
        // (stability=development); only the incubating marker surfaces it
        // (Phase B-2 stability filter).
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMeters("http.client")]
            internal static partial class HttpClientMeters;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpClientMeters.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("Histogram<long>")
            .And.Contain("unit: \"By\"");
    }

    [Fact]
    public void Emits_IncubatingMeters_For_Http_Client_Marker()
    {
        // Phase B-2 stability filter: the incubating marker surfaces
        // development-stability metrics under http.client (e.g.
        // http.client.request.body.size); the stable marker does not.
        const string incubatingSource = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingMeters("http.client")]
            internal static partial class HttpClientIncubatingMeters;
            """;

        const string stableSource = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionMeters("http.client")]
            internal static partial class HttpClientStableMeters;
            """;

        var incubating = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(incubatingSource)
            .RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpClientIncubatingMeters.g.cs", StringComparison.Ordinal))
            .ToString();

        var stable = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(stableSource)
            .RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpClientStableMeters.g.cs", StringComparison.Ordinal))
            .ToString();

        // Incubating surface includes development rows (active_requests,
        // connection.duration, open_connections, request.body.size,
        // response.body.size) plus the lone stable row (request.duration).
        incubating.Should()
            .Contain("CreateHttpClientRequestBodySizeHistogram")
            .And.Contain("CreateHttpClientResponseBodySizeHistogram")
            .And.Contain("CreateHttpClientActiveRequestsUpdowncounter")
            .And.Contain("CreateHttpClientOpenConnectionsUpdowncounter")
            .And.Contain("CreateHttpClientConnectionDurationHistogram")
            .And.Contain("CreateHttpClientRequestDurationHistogram");

        // Stable surface emits only the stable row.
        stable.Should()
            .Contain("CreateHttpClientRequestDurationHistogram")
            .And.NotContain("CreateHttpClientRequestBodySizeHistogram")
            .And.NotContain("CreateHttpClientResponseBodySizeHistogram")
            .And.NotContain("CreateHttpClientActiveRequestsUpdowncounter")
            .And.NotContain("CreateHttpClientOpenConnectionsUpdowncounter")
            .And.NotContain("CreateHttpClientConnectionDurationHistogram");
    }

    [Fact]
    public void Generated_Output_Is_Syntactically_Valid()
    {
        // The test-host compilation references Basic.Reference.Assemblies.Net100,
        // which does not surface System.Diagnostics.DiagnosticSource by default —
        // so CS0234 ("'Activity'/'Metrics' does not exist in 'System.Diagnostics'")
        // is expected here and proves nothing about generator correctness.
        // What we DO want to verify is that the generated source parses cleanly
        // (no CS1xxx/syntax errors) — that's an actionable gate on the emitter.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionMeters("http.server")]
            internal static partial class HttpServerMeters;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvMetersGenerator>(source);

        var syntaxErrors = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpServerMeters.g.cs", StringComparison.Ordinal))
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        syntaxErrors.Should().BeEmpty();
    }
}
