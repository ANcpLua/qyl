namespace qyl.instrumentation.generators.tests.Analyzers;

using Qyl.Instrumentation.Generators.Analyzers;
using Xunit;

public sealed class InlineSystemPromptAnalyzerTests
{
    [Fact]
    public void LongInstructionsInitializer_Warns()
    {
        var source = """
                     using Microsoft.Agents.AI;
                     public static class Factory
                     {
                         public static ChatClientAgentOptions Build() => new ChatClientAgentOptions
                         {
                             Instructions = "You are a precise triage assistant. Classify incidents by severity."
                         };
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<InlineSystemPromptAnalyzer>(source, "src/Factories/Factory.cs");

        Assert.Contains(diagnostics, static d => d.Id == InlineSystemPromptAnalyzer.DiagnosticId);
    }

    [Fact]
    public void MultilineInstructionsArgument_Warns()
    {
        var source = """"
                     using Microsoft.Agents.AI;
                     public static class Factory
                     {
                         public static ChatClientAgent Build() =>
                             new ChatClientAgent(new object(), instructions: """
                                 You are an expert.
                                 Output terse answers.
                                 """);
                     }
                     """";

        var diagnostics = AnalyzerTestHarness.Run<InlineSystemPromptAnalyzer>(source, "src/Factories/Factory.cs");

        Assert.Contains(diagnostics, static d => d.Id == InlineSystemPromptAnalyzer.DiagnosticId);
    }

    [Fact]
    public void ShortLiteralBelowThreshold_DoesNotWarn()
    {
        var source = """
                     using Microsoft.Agents.AI;
                     public static class Factory
                     {
                         public static ChatClientAgentOptions Build() => new ChatClientAgentOptions
                         {
                             Instructions = "test"
                         };
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<InlineSystemPromptAnalyzer>(source, "src/Factories/Factory.cs");

        Assert.DoesNotContain(diagnostics, static d => d.Id == InlineSystemPromptAnalyzer.DiagnosticId);
    }

    [Fact]
    public void TestsPath_IsExempt()
    {
        var source = """
                     using Microsoft.Agents.AI;
                     public static class Factory
                     {
                         public static ChatClientAgentOptions Build() => new ChatClientAgentOptions
                         {
                             Instructions = "You are a precise triage assistant. Classify incidents by severity."
                         };
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<InlineSystemPromptAnalyzer>(source, "tests/FactoryTests/Factory.cs");

        Assert.DoesNotContain(diagnostics, static d => d.Id == InlineSystemPromptAnalyzer.DiagnosticId);
    }
}
