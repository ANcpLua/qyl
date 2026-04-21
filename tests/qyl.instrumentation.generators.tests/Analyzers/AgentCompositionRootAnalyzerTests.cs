namespace qyl.instrumentation.generators.tests.Analyzers;

using Qyl.Instrumentation.Generators.Analyzers;
using Xunit;

public sealed class AgentCompositionRootAnalyzerTests
{
    [Fact]
    public void InlineConstruction_WithoutTelemetry_Warns()
    {
        var source = """
                     using Microsoft.Agents.AI;
                     public static class Caller
                     {
                         public static System.Threading.Tasks.Task Run()
                         {
                             var agent = new ChatClientAgent();
                             return agent.RunAsync();
                         }
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<AgentCompositionRootAnalyzer>(source);

        Assert.Contains(diagnostics, static d => d.Id == AgentCompositionRootAnalyzer.DiagnosticId);
    }

    [Fact]
    public void InlineConstructionDirectCall_Warns()
    {
        var source = """
                     using Microsoft.Agents.AI;
                     public static class Caller
                     {
                         public static System.Threading.Tasks.Task Run() =>
                             new ChatClientAgent().InvokeAsync();
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<AgentCompositionRootAnalyzer>(source);

        Assert.Contains(diagnostics, static d => d.Id == AgentCompositionRootAnalyzer.DiagnosticId);
    }

    [Fact]
    public void ReceiverFromFactory_DoesNotWarn()
    {
        var source = """
                     using Microsoft.Agents.AI;
                     public static class Caller
                     {
                         private static ChatClientAgent Create() => new ChatClientAgent();
                         public static System.Threading.Tasks.Task Run()
                         {
                             var agent = Create();
                             return agent.RunAsync();
                         }
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<AgentCompositionRootAnalyzer>(source);

        Assert.DoesNotContain(diagnostics, static d => d.Id == AgentCompositionRootAnalyzer.DiagnosticId);
    }

    [Fact]
    public void NonAgentReceiver_DoesNotWarn()
    {
        var source = """
                     public static class Caller
                     {
                         public static System.Threading.Tasks.Task RunAsync() =>
                             System.Threading.Tasks.Task.CompletedTask;
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<AgentCompositionRootAnalyzer>(source);

        Assert.DoesNotContain(diagnostics, static d => d.Id == AgentCompositionRootAnalyzer.DiagnosticId);
    }
}
