using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Byte-identity gate for generated semantic-convention surfaces. Each checked
/// signal/tier pair has an explicit snapshot under <c>Snapshots/</c>. The full
/// generator output for that marker must be byte-identical to the snapshot —
/// any drift in the emit pipeline (whitespace, member order, doc-comment shape)
/// fails the gate.
/// <para>
/// In addition, the contrib-parity tests assert that the emitted constant
/// member names and attribute key strings exactly match
/// <c>open-telemetry/opentelemetry-dotnet-contrib@55978aae5ae5641a0b405028db0d94de8d6f2a90</c>
/// for selected attributes present in the full Weaver-derived registry.
/// </para>
/// <para>
/// Complements <see cref="ByteIdentitySnapshotTests"/>, which keeps a focused
/// disk-prefix contrib-shape smoke while this class covers explicit
/// stable/incubating snapshots across the generated signal families.
/// </para>
/// </summary>
public sealed class ByteIdentityTests
{
    private const string ContribSha = "55978aae5ae5641a0b405028db0d94de8d6f2a90";

    [Theory]
    [InlineData("http", "HttpAttributes", false, "qyl.attributes.http.stable.expected.txt")]
    [InlineData("http", "HttpIncubatingAttributes", true, "qyl.attributes.http.incubating.expected.txt")]
    public void Attributes_File_Matches_Stability_Tier_Snapshot(
        string prefix,
        string className,
        bool incubating,
        string snapshotName)
    {
        var source = AttributeMarkerSource(prefix, className, incubating);

        var actual = RunAndGetGenerated<SemConvAttributesGenerator>(source, $"{className}.g.cs");
        var expected = LoadOrRegen(actual, snapshotName);

        actual.Should().Be(expected,
            $"emitted '{className}.g.cs' must be byte-identical to the {snapshotName} snapshot");
    }

    [Fact]
    public void Metrics_HttpServer_Stable_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionMetrics("http.server")]
            public static partial class HttpServerMetrics;
            """;

        var actual = RunAndGetGenerated<SemConvMetricsGenerator>(source, "HttpServerMetrics.g.cs");
        var expected = LoadOrRegen(actual,"qyl.metrics.http-server.stable.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpServerMetrics.g.cs' must be byte-identical to qyl.metrics.http-server.stable.expected.txt");
    }

    [Fact]
    public void Metrics_HttpServer_Incubating_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingMetrics("http.server")]
            public static partial class HttpServerIncubatingMetrics;
            """;

        var actual = RunAndGetGenerated<SemConvMetricsGenerator>(source, "HttpServerIncubatingMetrics.g.cs");
        var expected = LoadOrRegen(actual,"qyl.metrics.http-server.incubating.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpServerIncubatingMetrics.g.cs' must be byte-identical to qyl.metrics.http-server.incubating.expected.txt");
    }

    [Fact]
    public void Events_Exception_Stable_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionEvents("exception")]
            public static partial class ExceptionEvents;
            """;

        var actual = RunAndGetGenerated<SemConvEventsGenerator>(source, "ExceptionEvents.g.cs");
        var expected = LoadOrRegen(actual,"qyl.events.exception.stable.expected.txt");

        actual.Should().Be(expected,
            "emitted 'ExceptionEvents.g.cs' must be byte-identical to qyl.events.exception.stable.expected.txt");
    }

    [Fact]
    public void Events_Session_Incubating_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingEvents("session")]
            public static partial class SessionIncubatingEvents;
            """;

        var actual = RunAndGetGenerated<SemConvEventsGenerator>(source, "SessionIncubatingEvents.g.cs");
        var expected = LoadOrRegen(actual,"qyl.events.session.incubating.expected.txt");

        actual.Should().Be(expected,
            "emitted 'SessionIncubatingEvents.g.cs' must be byte-identical to qyl.events.session.incubating.expected.txt");
    }

    [Fact]
    public void Meters_HttpServer_Stable_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionMeters("http.server")]
            public static partial class HttpServerMeters;
            """;

        var actual = RunAndGetGenerated<SemConvMetersGenerator>(source, "HttpServerMeters.g.cs");
        var expected = LoadOrRegen(actual,"qyl.meters.http-server.stable.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpServerMeters.g.cs' must be byte-identical to qyl.meters.http-server.stable.expected.txt");
    }

    [Fact]
    public void Meters_HttpServer_Incubating_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingMeters("http.server")]
            public static partial class HttpServerIncubatingMeters;
            """;

        var actual = RunAndGetGenerated<SemConvMetersGenerator>(source, "HttpServerIncubatingMeters.g.cs");
        var expected = LoadOrRegen(actual,"qyl.meters.http-server.incubating.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpServerIncubatingMeters.g.cs' must be byte-identical to qyl.meters.http-server.incubating.expected.txt");
    }

    [Fact]
    public void Activities_Http_Stable_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionActivities("http")]
            public static partial class HttpActivityExtensions;
            """;

        var actual = RunAndGetGenerated<SemConvActivitiesGenerator>(source, "HttpActivityExtensions.g.cs");
        var expected = LoadOrRegen(actual,"qyl.activities.http.stable.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpActivityExtensions.g.cs' must be byte-identical to qyl.activities.http.stable.expected.txt");
    }

    [Fact]
    public void Activities_Http_Incubating_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingActivities("http")]
            public static partial class HttpIncubatingActivityExtensions;
            """;

        var actual = RunAndGetGenerated<SemConvActivitiesGenerator>(source, "HttpIncubatingActivityExtensions.g.cs");
        var expected = LoadOrRegen(actual,"qyl.activities.http.incubating.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpIncubatingActivityExtensions.g.cs' must be byte-identical to qyl.activities.http.incubating.expected.txt");
    }

    [Fact]
    public void Http_Contrib_Parity_Constant_Names_And_Keys()
    {
        // Contrib HttpAttributes.cs @ 55978aae:
        //     public const string AttributeHttpRequestMethod = "http.request.method";
        //     public const string AttributeHttpResponseStatusCode = "http.response.status_code";
        //     public const string AttributeHttpRoute = "http.route";
        // These remain the compact contrib-parity anchors; the tier snapshots
        // above cover the full generated http surface.
        var generated = RunAndGetGenerated<SemConvAttributesGenerator>(
            IncubatingMarkerSource("http", "HttpAttributes"), "HttpAttributes.g.cs");

        generated.Should()
            .Contain("public const string AttributeHttpRequestMethod = \"http.request.method\";")
            .And.Contain("public const string AttributeHttpResponseStatusCode = \"http.response.status_code\";")
            .And.Contain("public const string AttributeHttpRoute = \"http.route\";",
                $"member-region constants must match contrib@{ContribSha}");
    }

    [Fact]
    public void Network_Contrib_Parity_Constant_Name_And_Key()
    {
        // Contrib NetworkAttributes.cs @ 55978aae:
        //     public const string AttributeNetworkProtocolName = "network.protocol.name";
        var generated = RunAndGetGenerated<SemConvAttributesGenerator>(
            IncubatingMarkerSource("network", "NetworkAttributes"), "NetworkAttributes.g.cs");

        generated.Should()
            .Contain("public const string AttributeNetworkProtocolName = \"network.protocol.name\";",
                $"member-region constant must match contrib@{ContribSha}");
    }

    [Fact]
    public void Contrib_Sha_Pin_Is_Constant()
    {
        // Single grep target for the next Weaver-regen task. If the contrib
        // baseline ever shifts, find/replace this SHA across the snapshots
        // + ByteIdentitySnapshotTests + ByteIdentityTests in one pass.
        ContribSha.Should().Be("55978aae5ae5641a0b405028db0d94de8d6f2a90");
    }

    private static string AttributeMarkerSource(string prefix, string className, bool incubating)
    {
        var markerName = incubating
            ? "SemanticConventionIncubatingAttributes"
            : "SemanticConventionAttributes";

        return $$"""
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [{{markerName}}("{{prefix}}")]
            public static partial class {{className}};
            """;
    }

    private static string IncubatingMarkerSource(string prefix, string className) =>
        $$"""
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingAttributes("{{prefix}}")]
            public static partial class {{className}};
            """;

    private static string RunAndGetGenerated<TGenerator>(string source, string fileSuffix)
        where TGenerator : IIncrementalGenerator, new()
    {
        var result = GeneratorTestHelper.RunGenerator<TGenerator>(source);
        var tree = result.RunResult.GeneratedTrees
            .Single(t => t.FilePath.EndsWith(fileSuffix, StringComparison.Ordinal));
        return tree.ToString();
    }

    private static string LoadSnapshot(string resourceName)
    {
        var assembly = typeof(ByteIdentityTests).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Snapshot resource '{resourceName}' was not embedded into {assembly.FullName}. " +
                "Check the EmbeddedResource entry in qyl.opentelemetry.semconv.sourcegen.tests.csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// When <c>REGEN_SNAPSHOTS</c> env var is set to the absolute Snapshots/ dir,
    /// writes <paramref name="actual"/> to <c>{REGEN_SNAPSHOTS}/{resourceName}</c>
    /// and returns it (so the caller's <c>actual.Should().Be(expected)</c> passes
    /// trivially). When env var is empty, returns the embedded resource as usual.
    /// </summary>
    private static string LoadOrRegen(string actual, string resourceName)
    {
        var regenRoot = Environment.GetEnvironmentVariable("REGEN_SNAPSHOTS");
        if (!string.IsNullOrEmpty(regenRoot))
        {
            File.WriteAllText(Path.Combine(regenRoot, resourceName), actual);
            return actual;
        }
        return LoadSnapshot(resourceName);
    }
}
