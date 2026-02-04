using ANcpLua.Roslyn.Utilities.Testing;
using qyl.Analyzers.Analyzers;

namespace qyl.Analyzers.Tests.Analyzers;

/// <summary>
///     Tests for QYL012: Metric methods must be partial.
///     The [Counter] and [Histogram] attributes require methods to be partial for source generation.
/// </summary>
public sealed partial class Qyl012MetricMethodTests : AnalyzerTest<Qyl012MetricMethodMustBePartialAnalyzer>
{
    private const string Attributes = """
                                      namespace qyl.ServiceDefaults.Instrumentation;
                                      [System.AttributeUsage(System.AttributeTargets.Class)]
                                      public sealed class MeterAttribute(string name) : System.Attribute
                                      {
                                          public string Name { get; } = name;
                                      }
                                      [System.AttributeUsage(System.AttributeTargets.Method)]
                                      public sealed class CounterAttribute(string name) : System.Attribute
                                      {
                                          public string Name { get; } = name;
                                      }
                                      [System.AttributeUsage(System.AttributeTargets.Method)]
                                      public sealed class HistogramAttribute(string name) : System.Attribute
                                      {
                                          public string Name { get; } = name;
                                      }
                                      """;

    [Theory]
    [InlineData("public static void [|RecordOrder|]()")]
    [InlineData("internal static void [|RecordOrder|]()")]
    [InlineData("private static void [|RecordOrder|]()")]
    public Task ShouldReport_CounterMethodNotPartial(string methodDeclaration) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Counter("orders.created")]
                          {{methodDeclaration}} { }
                      }
                      """);

    [Theory]
    [InlineData("public static void [|RecordDuration|](double value)")]
    [InlineData("internal static void [|RecordDuration|](double value)")]
    public Task ShouldReport_HistogramMethodNotPartial(string methodDeclaration) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Histogram("request.duration")]
                          {{methodDeclaration}} { }
                      }
                      """);

    // Note: For the "no diagnostic" case, we use private partial methods which don't require implementation
    // until they are called (and our test doesn't call them)
    [Fact]
    public Task ShouldNotReport_PartialCounterMethod() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Counter("orders.created")]
                          static partial void RecordOrder();
                      }
                      """);

    [Fact]
    public Task ShouldNotReport_PartialHistogramMethod() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Histogram("request.duration")]
                          static partial void RecordDuration(double value);
                      }
                      """);

    [Fact]
    public Task ShouldNotReport_MethodWithoutMetricAttribute() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          public static void SomeMethod() { }
                      }
                      """);
}
