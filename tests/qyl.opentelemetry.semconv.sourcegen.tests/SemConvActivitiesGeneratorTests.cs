using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;


/// <summary>
/// PR-E tests: <c>[SemanticConventionActivities(&quot;&lt;prefix&gt;&quot;)]</c>
/// emits typed <c>Activity.SetTag</c> setter extensions per registry attribute.
/// </summary>
public sealed class SemConvActivitiesGeneratorTests
{
    [Fact]
    public void Emits_ActivitiesMarker_Attribute_PostInitialization()
    {
        const string source = "namespace Empty;";

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var attributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionActivitiesAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        attributeFile.Should()
            .Contain("namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;")
            .And.Contain("internal sealed class SemanticConventionActivitiesAttribute")
            .And.Contain("public SemanticConventionActivitiesAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }

    [Fact]
    public void Emits_StringSetter_For_HttpRoute()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class HttpActivityExtensions")
            .And.Contain("public static global::System.Diagnostics.Activity SetHttpRoute(")
            .And.Contain("this global::System.Diagnostics.Activity activity,")
            .And.Contain("string value)")
            .And.Contain("=> activity.SetTag(\"http.route\", value);");
    }

    [Fact]
    public void Maps_Int_Type_To_Long_Setter_For_StatusCode()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public static global::System.Diagnostics.Activity SetHttpResponseStatusCode(")
            .And.Contain("long value)")
            .And.Contain("=> activity.SetTag(\"http.response.status_code\", value);");
    }

    [Fact]
    public void Emits_Template_Setter_With_Segment_Parameter()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public static global::System.Diagnostics.Activity SetHttpRequestHeader(")
            .And.Contain("string segment,")
            .And.Contain("string[] value)")
            .And.Contain("=> activity.SetTag($\"http.request.header.{segment}\", value);");
    }

    [Fact]
    public void Enum_Attribute_Emits_Nested_Values_Class()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public static global::System.Diagnostics.Activity SetHttpRequestMethod(")
            .And.Contain("string value)")
            .And.Contain("=> activity.SetTag(\"http.request.method\", value);")
            .And.Contain("public static class HttpRequestMethodValues")
            .And.Contain("public const string Get = \"GET\";")
            .And.Contain("public const string Post = \"POST\";");
    }

    [Fact]
    public void Setter_Docs_Preserve_Contextual_Requirement_Metadata()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("/// Semantic-convention contexts:")
            .And.Contain("attributes.http.client (attribute_group, prefix <none>): required")
            .And.Contain("attributes.http.server (attribute_group, prefix <none>): required")
            .And.Contain("metric_attributes.http.server (attribute_group, prefix <none>): required")
            .And.Contain("conditionally_required - If and only if one was received/sent.");
    }

    [Fact]
    public void Stability_Gate_Deprecated_Attribute_Annotated_Obsolete()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("[global::System.Obsolete(\"Replaced by http.request.method.\")]")
            .And.Contain("SetHttpRequestMethodOriginal");
    }

    [Fact]
    public void Emits_IncubatingActivities_For_Http_Marker()
    {
        // Phase B-2 stability filter: the incubating marker surfaces
        // non-deprecated development-stability attributes under http (e.g.
        // http.connection.state, http.request.body.size); the stable marker
        // drops those (it keeps stable rows + all deprecated rows, regardless
        // of stability tier, so consumers can migrate at their pace).
        const string incubatingSource = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingActivities("http")]
            internal static partial class HttpIncubatingActivities;
            """;

        const string stableSource = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpStableActivities;
            """;

        var incubating = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(incubatingSource)
            .RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpIncubatingActivities.g.cs", StringComparison.Ordinal))
            .ToString();

        var stable = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(stableSource)
            .RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpStableActivities.g.cs", StringComparison.Ordinal))
            .ToString();

        // Incubating surface includes non-deprecated development rows.
        incubating.Should()
            .Contain("SetHttpConnectionState")
            .And.Contain("SetHttpRequestBodySize")
            .And.Contain("SetHttpResponseBodySize")
            .And.Contain("SetHttpRequestSize")
            .And.Contain("SetHttpResponseSize")
            // ...alongside the stable rows...
            .And.Contain("SetHttpRequestMethod")
            .And.Contain("SetHttpRoute")
            // ...and deprecated rows survive (audit: contrib/Java/Python parity).
            .And.Contain("SetHttpClientIp")
            .And.Contain("[global::System.Obsolete(\"Replaced by client.address.\")]");

        // Stable surface drops non-deprecated development rows...
        stable.Should()
            .NotContain("SetHttpConnectionState")
            .And.NotContain("SetHttpRequestBodySize")
            .And.NotContain("SetHttpResponseBodySize")
            .And.NotContain("SetHttpRequestSize")
            .And.NotContain("SetHttpResponseSize")
            // ...keeps stable rows...
            .And.Contain("SetHttpRequestMethod")
            .And.Contain("SetHttpRoute")
            .And.Contain("SetHttpResponseStatusCode")
            // ...and keeps deprecated rows for migration.
            .And.Contain("SetHttpClientIp")
            .And.Contain("[global::System.Obsolete(\"Replaced by client.address.\")]");
    }

    [Fact]
    public void Stable_Marker_Filters_NonStable_Enum_Members()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpStableActivities;

            [SemanticConventionIncubatingActivities("http")]
            internal static partial class HttpIncubatingActivities;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var stable = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpStableActivities.g.cs", StringComparison.Ordinal))
            .ToString();
        var incubating = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpIncubatingActivities.g.cs", StringComparison.Ordinal))
            .ToString();

        stable.Should()
            .Contain("public static class HttpRequestMethodValues")
            .And.NotContain("public const string Query = \"QUERY\";",
                "enum members must be filtered by the selected stability projection, not only by parent attribute stability");

        incubating.Should()
            .Contain("public static class HttpRequestMethodValues")
            .And.Contain("public const string Query = \"QUERY\";");
    }

    [Fact]
    public void Generated_Output_Is_Syntactically_Valid()
    {
        // See SemConvMetersGeneratorTests.Generated_Output_Is_Syntactically_Valid:
        // the test-host reference closure does not surface
        // System.Diagnostics.DiagnosticSource, so CS0234 on Activity is expected
        // and uninformative. The gate that catches generator bugs is whether the
        // generated source parses without CS1xxx-class errors.
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionActivities("http")]
            internal static partial class HttpActivityExtensions;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvActivitiesGenerator>(source);

        var syntaxErrors = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpActivityExtensions.g.cs", StringComparison.Ordinal))
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        syntaxErrors.Should().BeEmpty();
    }
}
