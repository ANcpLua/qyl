using ANcpLua.Roslyn.Utilities.Testing;
using qyl.Analyzers.Analyzers;

namespace qyl.Analyzers.Tests.Analyzers;

/// <summary>
///     Tests for QYL013: [Traced] attribute must have non-empty ActivitySourceName.
///     The ActivitySourceName is required for the source generator to create the ActivitySource.
/// </summary>
public sealed partial class Qyl013TracedTests : AnalyzerTest<Qyl013TracedActivitySourceNameAnalyzer>
{
    private const string TracedAttribute = """
                                           namespace qyl.ServiceDefaults.Instrumentation;
                                           [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Method)]
                                           public sealed class TracedAttribute : System.Attribute
                                           {
                                               public TracedAttribute() { }
                                               public TracedAttribute(string activitySourceName) => ActivitySourceName = activitySourceName;
                                               public string? ActivitySourceName { get; set; }
                                               public string? SpanName { get; set; }
                                           }
                                           """;

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public Task ShouldReport_EmptyOrWhitespaceActivitySourceName_OnClass(string name) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      [[|Traced("{{name}}")|]]
                      public class MyService { }
                      """);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public Task ShouldReport_EmptyOrWhitespaceActivitySourceName_OnMethod(string name) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      public class MyService
                      {
                          [[|Traced("{{name}}")|]]
                          public void DoWork() { }
                      }
                      """);

    [Theory]
    [InlineData("MyService")]
    [InlineData("qyl.MyService")]
    [InlineData("my-service")]
    public Task ShouldNotReport_ValidActivitySourceName_OnClass(string name) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      [Traced("{{name}}")]
                      public class MyService { }
                      """);

    [Theory]
    [InlineData("MyService")]
    [InlineData("qyl.MyService")]
    public Task ShouldNotReport_ValidActivitySourceName_OnMethod(string name) =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      public class MyService
                      {
                          [Traced("{{name}}")]
                          public void DoWork() { }
                      }
                      """);

    [Fact]
    public Task ShouldNotReport_TracedWithActivitySourceNameProperty() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      [Traced(ActivitySourceName = "MyService")]
                      public class MyService { }
                      """);

    [Fact]
    public Task ShouldNotReport_MethodInheritsFromClass() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      [Traced(ActivitySourceName = "MyService")]
                      public class MyService
                      {
                          // Method without [Traced] inherits from class - no diagnostic needed
                          public void DoWork() { }
                      }
                      """);

    [Fact]
    public Task ShouldReport_MethodWithSpanNameOnlyMissingActivitySource() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      [Traced(ActivitySourceName = "MyService")]
                      public class MyService
                      {
                          // Method has [Traced] but only SpanName - ActivitySourceName still needed per-method
                          [[|Traced(SpanName = "DoWork")|]]
                          public void DoWork() { }
                      }
                      """);

    [Fact]
    public Task ShouldNotReport_ClassWithoutTracedAttribute() =>
        VerifyAsync($$"""
                      using qyl.ServiceDefaults.Instrumentation;
                      {{TracedAttribute}}
                      public class MyService { }
                      """);
}
