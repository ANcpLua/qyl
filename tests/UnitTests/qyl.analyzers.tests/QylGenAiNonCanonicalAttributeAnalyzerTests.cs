// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp.Testing;
// using Microsoft.CodeAnalysis.Testing;
//
// namespace qyl.analyzers.tests;
//
// public class QylGenAiNonCanonicalAttributeAnalyzerTests
// {
//     private const string ActivityStub = """
//                                         namespace System.Diagnostics
//                                         {
//                                             public class Activity
//                                             {
//                                                 public Activity(string name) { }
//                                                 public Activity SetTag(string key, object? value) => this;
//                                             }
//                                         }
//                                         """;
//
//     private static CSharpAnalyzerTest<QylGenAiNonCanonicalAttributeAnalyzer, DefaultVerifier> CreateTest(
//         string testCode)
//     {
//         var test = new CSharpAnalyzerTest<QylGenAiNonCanonicalAttributeAnalyzer, DefaultVerifier>
//         {
//             TestCode = testCode + ActivityStub,
//             ReferenceAssemblies = ReferenceAssemblies.Net.Net80
//         };
//         return test;
//     }
//
//     [Fact]
//     public async Task NonCanonicalGenAiAttribute_ReportsDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.unknown.attribute", "value");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         test.ExpectedDiagnostics.Add(
//             new DiagnosticResult(QylGenAiNonCanonicalAttributeAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
//                 .WithLocation(8, 9)
//                 .WithArguments("gen_ai.unknown.attribute"));
//
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task CanonicalRequestModel_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.request.model", "gpt-4");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task CanonicalProviderName_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.provider.name", "openai");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task CanonicalUsageInputTokens_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.usage.input_tokens", 100);
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task CanonicalToolName_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.tool.name", "search");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task DeprecatedAttribute_NoDiagnostic_HandledByQYL001()
//     {
//
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.system", "openai");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task NonGenAiAttribute_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("http.method", "GET");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task CanonicalAgentId_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.agent.id", "agent-123");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task CanonicalConversationId_NoDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.conversation.id", "conv-456");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task NonCanonicalWithConstant_ReportsDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 private const string CustomAttr = "gen_ai.custom.made_up";
//
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag(CustomAttr, "value");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         test.ExpectedDiagnostics.Add(
//             new DiagnosticResult(QylGenAiNonCanonicalAttributeAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
//                 .WithLocation(10, 9)
//                 .WithArguments("gen_ai.custom.made_up"));
//
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
// }


