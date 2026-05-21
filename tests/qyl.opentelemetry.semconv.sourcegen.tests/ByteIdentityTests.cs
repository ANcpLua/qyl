using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Phase 2/4 byte-identity gate. Each domain in the embedded seed
/// resolved-registry has a paired <c>qyl.&lt;prefix&gt;.expected.txt</c> snapshot
/// under <c>Snapshots/</c>. The full generator output for that prefix must be
/// byte-identical to the snapshot — any drift in the emit pipeline (whitespace,
/// member order, doc-comment shape) fails the gate.
/// <para>
/// In addition, the contrib-parity tests assert that the emitted constant
/// member names and attribute key strings exactly match
/// <c>open-telemetry/opentelemetry-dotnet-contrib@55978aae5ae5641a0b405028db0d94de8d6f2a90</c>
/// for the attributes present in qyl's seed registry. Brief/note text-equivalence
/// across the full member region is a Phase 4 deliverable that depends on the
/// Weaver-generated registry replacing the hand-projected seed.
/// </para>
/// <para>
/// Complements agent 3's <see cref="ByteIdentitySnapshotTests"/>, which seeds
/// the disk-prefix smoke. This class extends coverage to http and network
/// (the other two prefixes in the seed registry) and adds the snapshot
/// round-trip + contrib parity dimension.
/// </para>
/// </summary>
public sealed class ByteIdentityTests
{
    private const string ContribSha = "55978aae5ae5641a0b405028db0d94de8d6f2a90";

    [Theory]
    [InlineData("disk", "DiskAttributes")]
    [InlineData("http", "HttpAttributes")]
    [InlineData("network", "NetworkAttributes")]
    public void Generated_File_Matches_Snapshot(string prefix, string className)
    {
        var source = $$"""
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionAttributes("{{prefix}}")]
            public static partial class {{className}};
            """;

        var actual = RunAndGetGenerated<SemConvAttributesGenerator>(source, $"{className}.g.cs");
        var expected = LoadOrRegen(actual,$"qyl.{prefix}.expected.txt");

        actual.Should().Be(expected,
            $"emitted '{className}.g.cs' must be byte-identical to the qyl.{prefix}.expected.txt snapshot");
    }

    [Fact]
    public void Metrics_HttpServer_Matches_Snapshot()
    {
        // Uses the Incubating marker so the snapshot covers all four v1.41.0
        // http.server.* rows (one stable + three development). Stability filter
        // is tested in SemConvMetricsGeneratorTests.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingMetrics("http.server")]
            public static partial class HttpServerMetrics;
            """;

        var actual = RunAndGetGenerated<SemConvMetricsGenerator>(source, "HttpServerMetrics.g.cs");
        var expected = LoadOrRegen(actual,"qyl.metrics.http-server.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpServerMetrics.g.cs' must be byte-identical to qyl.metrics.http-server.expected.txt");
    }

    [Fact]
    public void Events_Session_Matches_Snapshot()
    {
        // Uses the Incubating marker — session.start + session.end are both
        // development-stability in v1.41.0, so the stable surface would emit
        // an empty class. Stability filter coverage lives in
        // SemConvEventsGeneratorTests.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionIncubatingEvents("session")]
            public static partial class SessionEvents;
            """;

        var actual = RunAndGetGenerated<SemConvEventsGenerator>(source, "SessionEvents.g.cs");
        var expected = LoadOrRegen(actual,"qyl.events.session.expected.txt");

        actual.Should().Be(expected,
            "emitted 'SessionEvents.g.cs' must be byte-identical to qyl.events.session.expected.txt");
    }

    [Fact]
    public void Meters_HttpServer_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionMeters("http.server")]
            public static partial class HttpServerMeters;
            """;

        var actual = RunAndGetGenerated<SemConvMetersGenerator>(source, "HttpServerMeters.g.cs");
        var expected = LoadOrRegen(actual,"qyl.meters.http-server.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpServerMeters.g.cs' must be byte-identical to qyl.meters.http-server.expected.txt");
    }

    [Fact]
    public void Activities_Http_Matches_Snapshot()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionActivities("http")]
            public static partial class HttpActivityExtensions;
            """;

        var actual = RunAndGetGenerated<SemConvActivitiesGenerator>(source, "HttpActivityExtensions.g.cs");
        var expected = LoadOrRegen(actual,"qyl.activities.http.expected.txt");

        actual.Should().Be(expected,
            "emitted 'HttpActivityExtensions.g.cs' must be byte-identical to qyl.activities.http.expected.txt");
    }

    [Fact]
    public void Http_Contrib_Parity_Constant_Names_And_Keys()
    {
        // Contrib HttpAttributes.cs @ 55978aae:
        //     public const string AttributeHttpRequestMethod = "http.request.method";
        //     public const string AttributeHttpResponseStatusCode = "http.response.status_code";
        //     public const string AttributeHttpRoute = "http.route";
        // The seed registry covers exactly these three http attributes.
        var generated = RunAndGetGenerated<SemConvAttributesGenerator>(
            MarkerSource("http", "HttpAttributes"), "HttpAttributes.g.cs");

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
            MarkerSource("network", "NetworkAttributes"), "NetworkAttributes.g.cs");

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

    private static string MarkerSource(string prefix, string className) =>
        $$"""
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace OpenTelemetry.SemanticConventions;
            [SemanticConventionAttributes("{{prefix}}")]
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
