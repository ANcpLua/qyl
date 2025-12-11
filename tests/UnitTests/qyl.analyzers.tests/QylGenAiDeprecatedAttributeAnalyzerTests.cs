// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp.Testing;
// using Microsoft.CodeAnalysis.Testing;
//
// namespace qyl.analyzers.tests;
//
// public class QylGenAiDeprecatedAttributeAnalyzerTests
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
//     private static CSharpAnalyzerTest<QylGenAiDeprecatedAttributeAnalyzer, DefaultVerifier> CreateTest(string testCode)
//     {
//         var test = new CSharpAnalyzerTest<QylGenAiDeprecatedAttributeAnalyzer, DefaultVerifier>
//         {
//             TestCode = testCode + ActivityStub,
//             ReferenceAssemblies = ReferenceAssemblies.Net.Net80
//         };
//         return test;
//     }
//
//     [Fact]
//     public async Task DeprecatedGenAiSystem_ReportsDiagnostic()
//     {
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
//         test.ExpectedDiagnostics.Add(
//             new DiagnosticResult(QylGenAiDeprecatedAttributeAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
//                 .WithLocation(8, 9)
//                 .WithArguments("gen_ai.system", "gen_ai.provider.name"));
//
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task DeprecatedUsagePromptTokens_ReportsDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.usage.prompt_tokens", 100);
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         test.ExpectedDiagnostics.Add(
//             new DiagnosticResult(QylGenAiDeprecatedAttributeAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
//                 .WithLocation(8, 9)
//                 .WithArguments("gen_ai.usage.prompt_tokens", "gen_ai.usage.input_tokens"));
//
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task DeprecatedGenAiPrompt_ReportsDiagnosticWithDocLink()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag("gen_ai.prompt", "Hello");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         test.ExpectedDiagnostics.Add(
//             new DiagnosticResult(QylGenAiDeprecatedAttributeAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
//                 .WithLocation(8, 9)
//                 .WithArguments("gen_ai.prompt", "see QYL GenAI 1.38 documentation"));
//
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
//
//     [Fact]
//     public async Task NonDeprecatedAttribute_NoDiagnostic()
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
//     public async Task ConstantUsedForDeprecatedAttribute_ReportsDiagnostic()
//     {
//         const string text = """
//                             using System.Diagnostics;
//
//                             public class Program
//                             {
//                                 private const string DeprecatedAttr = "gen_ai.system";
//
//                                 public void Main()
//                                 {
//                                     var activity = new Activity("test");
//                                     activity.SetTag(DeprecatedAttr, "openai");
//                                 }
//                             }
//                             """;
//
//         var test = CreateTest(text);
//         test.ExpectedDiagnostics.Add(
//             new DiagnosticResult(QylGenAiDeprecatedAttributeAnalyzer.DiagnosticId, DiagnosticSeverity.Warning)
//                 .WithLocation(10, 9)
//                 .WithArguments("gen_ai.system", "gen_ai.provider.name"));
//
//         await test.RunAsync(TestContext.Current.CancellationToken);
//     }
// }


