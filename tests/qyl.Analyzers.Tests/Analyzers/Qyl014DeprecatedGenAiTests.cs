using ANcpLua.Roslyn.Utilities.Testing;
using qyl.Analyzers.Analyzers;

namespace qyl.Analyzers.Tests.Analyzers;

/// <summary>
///     Tests for QYL014: Deprecated GenAI semantic convention attributes.
///     The analyzer detects deprecated GenAI attribute names and suggests replacements.
/// </summary>
public sealed partial class Qyl014DeprecatedGenAiTests : AnalyzerTest<Qyl014DeprecatedGenAiAttributeAnalyzer>
{
    [Theory]
    [InlineData("gen_ai.prompt.tokens", "gen_ai.usage.input_tokens")]
    [InlineData("gen_ai.completion.tokens", "gen_ai.usage.output_tokens")]
    [InlineData("gen_ai.model", "gen_ai.request.model")]
    public Task ShouldReport_DeprecatedAttributeInSetTag(string deprecated, string _) =>
        VerifyAsync($$"""
                      using System.Diagnostics;
                      public class Test
                      {
                          public void Record(Activity activity)
                          {
                              activity.SetTag([|"{{deprecated}}"|], 100);
                          }
                      }
                      """);

    [Theory]
    [InlineData("gen_ai.prompt.tokens")]
    [InlineData("gen_ai.completion.tokens")]
    public Task ShouldReport_DeprecatedAttributeInAddTag(string deprecated) =>
        VerifyAsync($$"""
                      using System.Diagnostics;
                      public class Test
                      {
                          public void Record(Activity activity)
                          {
                              activity.AddTag([|"{{deprecated}}"|], 100);
                          }
                      }
                      """);

    [Theory]
    [InlineData("gen_ai.prompt.tokens")]
    [InlineData("gen_ai.model")]
    public Task ShouldReport_DeprecatedAttributeInDictionaryIndexer(string deprecated) =>
        VerifyAsync($$"""
                      using System.Collections.Generic;
                      public class Test
                      {
                          public void Record()
                          {
                              var tags = new Dictionary<string, object>();
                              tags[[|"{{deprecated}}"|]] = 100;
                          }
                      }
                      """);

    [Fact]
    public Task ShouldReport_DeprecatedAttributeInInitializer() =>
        VerifyAsync("""
                    using System.Collections.Generic;
                    public class Test
                    {
                        public void Record()
                        {
                            var tags = new Dictionary<string, object>
                            {
                                { [|"gen_ai.prompt.tokens"|], 100 }
                            };
                        }
                    }
                    """);

    [Fact]
    public Task ShouldNotReport_CurrentAttributeNames() =>
        VerifyAsync("""
                    using System.Diagnostics;
                    public class Test
                    {
                        public void Record(Activity activity)
                        {
                            activity.SetTag("gen_ai.usage.input_tokens", 100);
                            activity.SetTag("gen_ai.usage.output_tokens", 50);
                            activity.SetTag("gen_ai.request.model", "gpt-4");
                            activity.SetTag("gen_ai.operation.name", "chat");
                        }
                    }
                    """);

    [Fact]
    public Task ShouldNotReport_DeprecatedStringInNonTelemetryContext() =>
        VerifyAsync("""
                    public class Test
                    {
                        // String in a non-telemetry context should not be flagged
                        private const string Message = "gen_ai.prompt.tokens is deprecated";

                        public string GetMessage() => "gen_ai.prompt.tokens";
                    }
                    """);

    [Fact]
    public Task ShouldNotReport_UnrelatedMethodNames() =>
        VerifyAsync("""
                    public class Test
                    {
                        // Method names like "Setup" or "AddItem" should not trigger
                        public void Setup(string key, int value)
                        {
                            // This should not flag since "Setup" is not a telemetry method
                        }

                        public void AddItem(string key)
                        {
                            // "AddItem" should not match as telemetry method
                        }
                    }
                    """);
}
