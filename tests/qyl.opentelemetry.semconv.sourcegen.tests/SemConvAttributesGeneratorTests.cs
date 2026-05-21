using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration.Tests;

public sealed class SemConvAttributesGeneratorTests
{
    [Fact]
    public void Emits_Marker_Attribute_PostInitialization()
    {
        const string source = """
            namespace Empty;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var attributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionAttributesAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        attributeFile.Should()
            .Contain("namespace Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;")
            .And.Contain("internal sealed class SemanticConventionAttributesAttribute")
            .And.Contain("public SemanticConventionAttributesAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");

        var incubatingAttributeFile = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("SemanticConventionIncubatingAttributesAttribute.g.cs", StringComparison.Ordinal))
            .ToString();

        incubatingAttributeFile.Should()
            .Contain("internal sealed class SemanticConventionIncubatingAttributesAttribute")
            .And.Contain("public SemanticConventionIncubatingAttributesAttribute(string prefix)")
            .And.Contain("Conditional(\"QYL_SEMCONV_USAGES\")");
    }

    [Fact]
    public void Emits_DiskAttributes_For_Disk_Incubating_Marker()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingAttributes("disk")]
            internal static partial class DiskAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("DiskAttributes.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("namespace MyApp;")
            .And.Contain("static partial class DiskAttributes")
            .And.Contain("public const string AttributeDiskIoDirection = \"disk.io.direction\";")
            .And.Contain("public static class DiskIoDirectionValues")
            .And.Contain("public const string Read = \"read\";")
            .And.Contain("public const string Write = \"write\";");
    }

    [Fact]
    public void Emits_HttpAttributes_For_Http_Marker_With_Three_Constants()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionAttributes("http")]
            internal static partial class HttpAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpAttributes.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public const string AttributeHttpRequestMethod = \"http.request.method\";")
            .And.Contain("public const string AttributeHttpResponseStatusCode = \"http.response.status_code\";")
            .And.Contain("public const string AttributeHttpRoute = \"http.route\";");

        generated.Should()
            .NotContain("AttributeHttpConnectionState",
                "http.connection.state is development-stability and must not appear under [SemanticConventionAttributes]")
            .And.NotContain("AttributeHttpRequestBodySize",
                "http.request.body.size is development-stability and must not appear under [SemanticConventionAttributes]")
            .And.NotContain("AttributeHttpResponseBodySize",
                "http.response.body.size is development-stability and must not appear under [SemanticConventionAttributes]")
            .And.Contain("AttributeHttpClientIp",
                "deprecated migration symbols survive stable projection until upstream removes them");
    }

    [Fact]
    public void Incubating_Marker_Emits_All_Http_Attributes_As_Superset()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingAttributes("http")]
            internal static partial class HttpIncubatingAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpIncubatingAttributes.g.cs", StringComparison.Ordinal))
            .ToString();

        generated.Should()
            .Contain("public const string AttributeHttpRequestMethod = \"http.request.method\";")
            .And.Contain("public const string AttributeHttpRoute = \"http.route\";")
            .And.Contain("public const string AttributeHttpConnectionState = \"http.connection.state\";")
            .And.Contain("public const string AttributeHttpRequestBodySize = \"http.request.body.size\";")
            .And.Contain("public const string AttributeHttpResponseBodySize = \"http.response.body.size\";")
            .And.Contain("public const string AttributeHttpClientIp = \"http.client_ip\";");
    }

    [Fact]
    public void Stable_Marker_Filters_NonStable_Enum_Members()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionAttributes("http")]
            internal static partial class HttpStableAttributes;

            [SemanticConventionIncubatingAttributes("http")]
            internal static partial class HttpIncubatingAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var stable = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpStableAttributes.g.cs", StringComparison.Ordinal))
            .ToString();
        var incubating = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("HttpIncubatingAttributes.g.cs", StringComparison.Ordinal))
            .ToString();

        stable.Should()
            .Contain("public static class HttpRequestMethodValues")
            .And.NotContain("public const string Query = \"QUERY\";",
                "http.request.method is stable but its QUERY enum member is development-stability in semconv v1.41.0");

        incubating.Should()
            .Contain("public static class HttpRequestMethodValues")
            .And.Contain("public const string Query = \"QUERY\";");
    }

    [Fact]
    public void Stable_Marker_Filters_Db_System_Development_Enum_Members()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionAttributes("db")]
            internal static partial class DbStableAttributes;

            [SemanticConventionIncubatingAttributes("db")]
            internal static partial class DbIncubatingAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var stable = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("DbStableAttributes.g.cs", StringComparison.Ordinal))
            .ToString();
        var incubating = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("DbIncubatingAttributes.g.cs", StringComparison.Ordinal))
            .ToString();

        var stableValues = ExtractNestedClass(stable, "DbSystemNameValues");
        var incubatingValues = ExtractNestedClass(incubating, "DbSystemNameValues");

        stableValues.Should()
            .Contain("public static class DbSystemNameValues")
            .And.NotContain("public const string Redis = \"redis\";")
            .And.NotContain("public const string Mongodb = \"mongodb\";")
            .And.NotContain("public const string Cassandra = \"cassandra\";");

        incubatingValues.Should()
            .Contain("public static class DbSystemNameValues")
            .And.Contain("public const string Redis = \"redis\";")
            .And.Contain("public const string Mongodb = \"mongodb\";")
            .And.Contain("public const string Cassandra = \"cassandra\";");
    }

    [Fact]
    public void Compilation_With_Generator_Has_No_Errors()
    {
        const string source = """
            using Qyl.OpenTelemetry.SemanticConventions.SourceGeneration;

            namespace MyApp;

            [SemanticConventionIncubatingAttributes("disk")]
            internal static partial class DiskAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var errors = result.OutputCompilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(static d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errors.Should().BeEmpty();
    }

    private static string ExtractNestedClass(string generated, string className)
    {
        var marker = "    public static class " + className;
        var start = generated.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"generated source should contain nested class {className}");

        var endMarker = "\n    }\n";
        var end = generated.IndexOf(endMarker, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"generated source should close nested class {className}");

        return generated.Substring(start, end - start + endMarker.Length);
    }
}
