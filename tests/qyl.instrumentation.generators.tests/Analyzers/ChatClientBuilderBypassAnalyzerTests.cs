namespace qyl.instrumentation.generators.tests.Analyzers;

using Qyl.Instrumentation.Generators.Analyzers;
using Xunit;

public sealed class ChatClientBuilderBypassAnalyzerTests
{
    [Fact]
    public void OpenAIClient_InServiceFile_Warns()
    {
        var source = """
                     using OpenAI;
                     public static class Service
                     {
                         public static object Create() => new OpenAIClient("key");
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<ChatClientBuilderBypassAnalyzer>(
            source, "src/qyl.mcp/Agents/AgentLlmFactory.cs");

        Assert.Contains(diagnostics, static d => d.Id == ChatClientBuilderBypassAnalyzer.DiagnosticId);
    }

    [Fact]
    public void AnthropicClient_InServiceFile_Warns()
    {
        var source = """
                     using Anthropic;
                     public static class Service
                     {
                         public static object Create() => new AnthropicClient();
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<ChatClientBuilderBypassAnalyzer>(
            source, "src/qyl.mcp/Services/MyService.cs");

        Assert.Contains(diagnostics, static d => d.Id == ChatClientBuilderBypassAnalyzer.DiagnosticId);
    }

    [Fact]
    public void InsideChatClientBuilder_DoesNotWarn()
    {
        var source = """
                     using OpenAI;
                     public static class Builder
                     {
                         public static object Create() => new OpenAIClient("key");
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<ChatClientBuilderBypassAnalyzer>(
            source, "src/qyl.mcp/Clients/OpenAIChatClientBuilder.cs");

        Assert.DoesNotContain(diagnostics, static d => d.Id == ChatClientBuilderBypassAnalyzer.DiagnosticId);
    }

    [Fact]
    public void InsideChatClientFactory_DoesNotWarn()
    {
        var source = """
                     using OllamaSharp;
                     public static class Factory
                     {
                         public static object Create() => new OllamaApiClient();
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<ChatClientBuilderBypassAnalyzer>(
            source, "src/qyl.mcp/Factories/OllamaChatClientFactory.cs");

        Assert.DoesNotContain(diagnostics, static d => d.Id == ChatClientBuilderBypassAnalyzer.DiagnosticId);
    }

    [Fact]
    public void TestsPath_IsExempt()
    {
        var source = """
                     using OpenAI;
                     public static class Service
                     {
                         public static object Create() => new OpenAIClient("key");
                     }
                     """;

        var diagnostics = AnalyzerTestHarness.Run<ChatClientBuilderBypassAnalyzer>(
            source, "tests/qyl.mcp.tests/Fakes/FakeService.cs");

        Assert.DoesNotContain(diagnostics, static d => d.Id == ChatClientBuilderBypassAnalyzer.DiagnosticId);
    }
}
