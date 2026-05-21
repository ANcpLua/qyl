using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// §2.1 namespace-parameterization invariant per the otel-shape-strategist plan:
/// the emit logic must not branch on the target namespace. The class marked
/// with <c>[SemanticConventionAttributes("&lt;prefix&gt;")]</c> may live in any
/// namespace the consumer chooses (an upstream design choice: contrib emits a
/// fixed <c>OpenTelemetry.SemanticConventions</c> namespace, but qyl's
/// marker-attribute pattern lets the consumer own placement). When two
/// otherwise-identical sources differ only by the namespace they declare the
/// marked class in, the generator output must differ only by the corresponding
/// <c>namespace</c> declaration line.
/// </summary>
public sealed class NamespaceParameterizationTest
{
    [Fact]
    public void Namespace_Change_Only_Affects_Namespace_Declaration()
    {
        const string sourceA = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace ConsumerA.Telemetry;
            [SemanticConventionAttributes("disk")]
            public static partial class DiskAttributes;
            """;

        const string sourceB = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            namespace ConsumerB.Different.Nested.Path;
            [SemanticConventionAttributes("disk")]
            public static partial class DiskAttributes;
            """;

        var emittedA = RunAndGetGenerated(sourceA);
        var emittedB = RunAndGetGenerated(sourceB);

        // Normalize both outputs by stripping the single `namespace ...;` line
        // and the following blank line. The remainder must be byte-identical.
        var bodyA = StripNamespaceDeclaration(emittedA);
        var bodyB = StripNamespaceDeclaration(emittedB);

        bodyA.Should().Be(bodyB,
            "the only diff between two generator runs with different target namespaces must be the namespace declaration line");

        emittedA.Should().Contain("namespace ConsumerA.Telemetry;");
        emittedB.Should().Contain("namespace ConsumerB.Different.Nested.Path;");
    }

    [Fact]
    public void Empty_Global_Namespace_Omits_Namespace_Declaration()
    {
        // The MarkerExtractor maps IsGlobalNamespace → empty string, and the
        // emitter then skips the namespace declaration entirely. Lock this in
        // so a future refactor doesn't accidentally emit `namespace ;` or
        // `namespace global::;`.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
            [SemanticConventionAttributes("disk")]
            public static partial class DiskAttributes;
            """;

        var emitted = RunAndGetGenerated(source);

        emitted.Should().NotContain("namespace ;");
        emitted.Should().NotContain("namespace global::;");
        emitted.Should().Contain("static partial class DiskAttributes",
            "the class body must still emit when the marker is in the global namespace");
    }

    private static string RunAndGetGenerated(string source)
    {
        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);
        return result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("DiskAttributes.g.cs", StringComparison.Ordinal))
            .ToString();
    }

    private static string StripNamespaceDeclaration(string source)
    {
        var lines = source.Split('\n');
        var kept = new List<string>(lines.Length);
        var skipNext = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("namespace ", StringComparison.Ordinal))
            {
                // Skip this line and the blank line that follows (per the emitter).
                skipNext = true;
                continue;
            }
            if (skipNext && string.IsNullOrWhiteSpace(line.TrimEnd('\r')))
            {
                skipNext = false;
                continue;
            }
            skipNext = false;
            kept.Add(line);
        }
        return string.Join('\n', kept);
    }
}
