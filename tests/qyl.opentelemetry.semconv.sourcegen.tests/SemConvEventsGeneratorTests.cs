using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

public sealed class SemConvEventsGeneratorTests
{
    [Fact]
    public void Emits_Marker_Attribute_PostInitialization()
    {
        const string source = """
            namespace Empty;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventsGenerator>(source);

        var attributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionEventsAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        attributeFile.Should()
            .Contain("namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;")
            .And.Contain("internal sealed class SemanticConventionEventsAttribute")
            .And.Contain("public SemanticConventionEventsAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }

    [Fact]
    public void Stable_Marker_Emits_Only_Stable_Exception_Event()
    {
        // Only `exception` is stable in v1.41.0 — the stable surface for prefix "exception"
        // must emit it; all other event prefixes (session.*, gen_ai.*, http.*.exception, ...)
        // are non-stable and must yield empty stable surfaces.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionEvents("exception")]
            internal static partial class ExceptionEvents;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("ExceptionEvents.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class ExceptionEvents")
            .And.Contain("public const string EventException = \"exception\";")
            .And.Contain("public readonly record struct ExceptionPayload");
    }

    [Fact]
    public void Stable_Marker_Filters_Out_Development_Session_Events()
    {
        // session.start + session.end are both development-stability in v1.41.0;
        // the stable surface for "session" prefix must therefore emit zero events.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionEvents("session")]
            internal static partial class SessionStableEvents;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SessionStableEvents.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("static partial class SessionStableEvents")
            .And.NotContain("public const string Event",
                "session.* events are development-stability and must not appear on the stable surface")
            .And.NotContain("public readonly record struct",
                "no payload struct should be emitted when stability filter removes every event");
    }

    [Fact]
    public void Incubating_Marker_Emits_Development_Session_Events()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingEvents("session")]
            internal static partial class SessionIncubatingEvents;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SessionIncubatingEvents.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class SessionIncubatingEvents")
            .And.Contain("public const string EventSessionStart = \"session.start\";")
            .And.Contain("public const string EventSessionEnd = \"session.end\";")
            .And.Contain("public readonly record struct SessionStartPayload")
            .And.Contain("public string SessionId { get; init; }")
            .And.Contain("public string? SessionPreviousId { get; init; }");
    }

    [Fact]
    public void Incubating_Marker_Emits_ReleaseCandidate_FeatureFlag_Event()
    {
        // feature_flag.evaluation is release_candidate in v1.41.0 — the incubating
        // surface (AllStabilities) must include it. The stable surface (StableOnly)
        // would not, since release_candidate != stable.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingEvents("feature_flag")]
            internal static partial class FeatureFlagIncubatingEvents;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventsGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("FeatureFlagIncubatingEvents.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public const string EventFeatureFlagEvaluation = \"feature_flag.evaluation\";")
            .And.Contain("public readonly record struct FeatureFlagEvaluationPayload")
            .And.Contain("FeatureFlagKey");
    }

    [Fact]
    public void Compilation_With_Generator_Has_No_Errors()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionEvents("exception")]
            internal static partial class ExceptionEvents;

            [SemanticConventionIncubatingEvents("session")]
            internal static partial class SessionIncubatingEvents;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvEventsGenerator>(source);

        var errors = result.OutputCompilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty();
    }
}
