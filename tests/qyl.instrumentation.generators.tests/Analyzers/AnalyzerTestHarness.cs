// Copyright (c) 2025-2026 ancplua

namespace qyl.instrumentation.generators.tests.Analyzers;

using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
///     Minimal analyzer test harness — compiles source with fake Microsoft.Agents.AI /
///     provider-SDK stubs, runs the given analyzer, returns emitted diagnostics.
/// </summary>
internal static class AnalyzerTestHarness
{
    public const string AgentStubs = """
                                     namespace Microsoft.Agents.AI
                                     {
                                         public class AIAgent
                                         {
                                             public virtual System.Threading.Tasks.Task RunAsync() => System.Threading.Tasks.Task.CompletedTask;
                                             public virtual System.Threading.Tasks.Task InvokeAsync() => System.Threading.Tasks.Task.CompletedTask;
                                             public virtual System.Threading.Tasks.Task CreateSessionAsync() => System.Threading.Tasks.Task.CompletedTask;
                                         }

                                         public sealed class ChatClientAgentOptions
                                         {
                                             public string? Instructions { get; set; }
                                         }

                                         public class ChatClientAgent : AIAgent
                                         {
                                             public ChatClientAgent() { }
                                             public ChatClientAgent(ChatClientAgentOptions options) { }
                                             public ChatClientAgent(object client, string? instructions = null) { }
                                         }

                                         public class DelegatingAIAgent : AIAgent { }
                                     }

                                     namespace OpenAI { public sealed class OpenAIClient { public OpenAIClient(string apiKey) { } } }
                                     namespace Azure.AI.OpenAI { public sealed class AzureOpenAIClient { public AzureOpenAIClient() { } } }
                                     namespace Anthropic { public sealed class AnthropicClient { public AnthropicClient() { } } }
                                     namespace OllamaSharp { public sealed class OllamaApiClient { public OllamaApiClient() { } } }
                                     """;

    public static ImmutableArray<Diagnostic> Run<TAnalyzer>(string source, string filePath = "Test.cs")
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: filePath);
        var stubTree = CSharpSyntaxTree.ParseText(AgentStubs, path: "Stubs.cs");

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };
        TryAddReference(references, Path.Join(runtimeDir, "System.Runtime.dll"));
        TryAddReference(references, Path.Join(runtimeDir, "netstandard.dll"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree, stubTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new TAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    // Optional runtime ref — some SDK layouts omit netstandard.dll / System.Runtime.dll
    // alongside each other. Surface the miss via Trace so a failing test has a breadcrumb,
    // but don't abort: the analyzer under test only needs core references.
    private static void TryAddReference(List<MetadataReference> references, string path)
    {
        try { references.Add(MetadataReference.CreateFromFile(path)); }
        catch (FileNotFoundException ex)
        {
            System.Diagnostics.Trace.WriteLine($"optional ref skipped: {path} ({ex.Message})");
        }
    }
}
