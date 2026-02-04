using ANcpLua.Roslyn.Utilities.Testing;
using qyl.Analyzers.Analyzers;

namespace qyl.Analyzers.Tests.Analyzers;

/// <summary>
///     Tests for QYL015: High-cardinality metric tags.
///     The analyzer warns about tags that could cause cardinality explosion in metrics backends.
/// </summary>
public sealed partial class Qyl015HighCardinalityTests : AnalyzerTest<Qyl015HighCardinalityMetricTagAnalyzer>
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
                                      [System.AttributeUsage(System.AttributeTargets.Parameter)]
                                      public sealed class TagAttribute(string name) : System.Attribute
                                      {
                                          public string Name { get; } = name;
                                      }
                                      """;

    [Theory]
    [InlineData("user.id")]
    [InlineData("user_id")]
    [InlineData("userId")]
    [InlineData("request.id")]
    [InlineData("session.id")]
    [InlineData("trace.id")]
    public Task ShouldReport_HighCardinalityTagOnCounter(string tagName) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Counter("requests.count")]
                          static partial void RecordRequest([|[Tag("{{tagName}}")] string id|]);
                      }
                      """);

    [Theory]
    [InlineData("customer.id")]
    [InlineData("order.id")]
    [InlineData("email")]
    [InlineData("ip")]
    public Task ShouldReport_HighCardinalityTagOnHistogram(string tagName) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Histogram("request.duration")]
                          static partial void RecordDuration(double ms, [|[Tag("{{tagName}}")] string value|]);
                      }
                      """);

    [Theory]
    [InlineData("custom.user.id")]
    [InlineData("my_user_id")]
    [InlineData("client.email")]
    public Task ShouldReport_HighCardinalityTagWithPrefix(string tagName) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Counter("events.count")]
                          static partial void RecordEvent([|[Tag("{{tagName}}")] string value|]);
                      }
                      """);

    [Theory]
    [InlineData("status")]
    [InlineData("http.method")]
    [InlineData("http.status_code")]
    [InlineData("service.name")]
    [InlineData("environment")]
    [InlineData("region")]
    [InlineData("user.type")]
    [InlineData("order.type")]
    public Task ShouldNotReport_LowCardinalityTags(string tagName) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Counter("requests.count")]
                          static partial void RecordRequest([Tag("{{tagName}}")] string value);
                      }
                      """);

    [Fact]
    public Task ShouldNotReport_TagOnNonMetricMethod() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          // Method without [Counter] or [Histogram] - tags are fine
                          public static void ProcessUser([Tag("user.id")] string userId) { }
                      }
                      """);

    [Fact]
    public Task ShouldNotReport_ParameterWithoutTagAttribute() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{Attributes}}
                      [Meter("App")]
                      public static partial class AppMetrics
                      {
                          [Counter("requests.count")]
                          static partial void RecordRequest(string userId);
                      }
                      """);
}
