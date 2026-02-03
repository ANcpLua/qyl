using ANcpLua.Roslyn.Utilities.Testing;
using qyl.Analyzers.Analyzers;

namespace qyl.Analyzers.Tests.Analyzers;

/// <summary>
/// Tests for QYL011: Meter class must be partial static.
/// The [Meter] attribute requires the class to be partial static for source generation.
/// </summary>
public sealed partial class Qyl011MeterClassTests : AnalyzerTest<Qyl011MeterClassMustBePartialStaticAnalyzer>
{
    private const string MeterAttribute = """
        namespace qyl.ServiceDefaults.Instrumentation;
        [System.AttributeUsage(System.AttributeTargets.Class)]
        public sealed class MeterAttribute(string name) : System.Attribute
        {
            public string Name { get; } = name;
            public string? Version { get; set; }
        }
        """;

    [Theory]
    [InlineData("class [|AppMetrics|]")]
    [InlineData("public class [|AppMetrics|]")]
    [InlineData("internal class [|AppMetrics|]")]
    public Task ShouldReport_NonPartialNonStaticClass(string declaration) =>
        VerifyAsync($$"""
            using qyl.ServiceDefaults.Instrumentation;
            {{MeterAttribute}}
            [Meter("App")]
            {{declaration}} { }
            """);

    [Theory]
    [InlineData("static class [|AppMetrics|]")]
    [InlineData("public static class [|AppMetrics|]")]
    public Task ShouldReport_StaticButNotPartial(string declaration) =>
        VerifyAsync($$"""
            using qyl.ServiceDefaults.Instrumentation;
            {{MeterAttribute}}
            [Meter("App")]
            {{declaration}} { }
            """);

    [Theory]
    [InlineData("partial class [|AppMetrics|]")]
    [InlineData("public partial class [|AppMetrics|]")]
    public Task ShouldReport_PartialButNotStatic(string declaration) =>
        VerifyAsync($$"""
            using qyl.ServiceDefaults.Instrumentation;
            {{MeterAttribute}}
            [Meter("App")]
            {{declaration}} { }
            """);

    [Theory]
    [InlineData("static partial class AppMetrics")]
    [InlineData("public static partial class AppMetrics")]
    [InlineData("internal static partial class AppMetrics")]
    public Task ShouldNotReport_PartialStaticClass(string declaration) =>
        VerifyAsync($$"""
            using qyl.ServiceDefaults.Instrumentation;
            {{MeterAttribute}}
            [Meter("App")]
            {{declaration}} { }
            """);

    [Fact]
    public Task ShouldNotReport_ClassWithoutMeterAttribute() =>
        VerifyAsync($$"""
            using qyl.ServiceDefaults.Instrumentation;
            {{MeterAttribute}}
            public class AppMetrics { }
            """);
}
