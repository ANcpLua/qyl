using System.Collections.Immutable;
using System.Text;
using Qyl.ServiceDefaults.Generator.Models;
using Microsoft.CodeAnalysis.CSharp;

namespace Qyl.ServiceDefaults.Generator.Emitters;

/// <summary>
///     Emits interceptor source code for GenAI SDK method invocations.
/// </summary>
/// <remarks>
///     Uses <see cref="ProviderRegistry" /> as the Single Source of Truth for provider definitions.
/// </remarks>
internal static class GenAiInterceptorEmitter
{
    /// <summary>
    ///     Emits the interceptor source code for all GenAI invocations.
    /// </summary>
    public static string Emit(ImmutableArray<GenAiCallSite> invocations)
    {
        if (invocations.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();

        EmitterHelpers.AppendFileHeader(sb, suppressWarnings: true);
        AppendUsings(sb);
        EmitterHelpers.AppendInterceptsLocationAttribute(sb);
        AppendClassOpen(sb);
        AppendInterceptorMethods(sb, invocations);
        EmitterHelpers.AppendClassClose(sb);

        return sb.ToString();
    }


    private static void AppendUsings(StringBuilder sb)
    {
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using Qyl.ServiceDefaults.Instrumentation;");
        sb.AppendLine("using Qyl.ServiceDefaults.Instrumentation.GenAi;");
        sb.AppendLine("using qyl.protocol.Attributes;");
        sb.AppendLine();
    }

    private static void AppendClassOpen(StringBuilder sb)
    {
        sb.AppendLine("""
                      namespace Qyl.ServiceDefaults.Generator
                      {
                          file static class GenAiInterceptors
                          {
                      """);
    }

    private static void AppendInterceptorMethods(
        StringBuilder sb,
        ImmutableArray<GenAiCallSite> invocations)
    {
        var orderedInvocations = invocations
            .OrderBy(static i => i.SortKey, StringComparer.Ordinal);

        var index = 0;
        foreach (var invocation in orderedInvocations)
        {
            if (invocation.IsStreaming)
                AppendStreamingInterceptor(sb, invocation, index);
            else
                AppendSingleInterceptor(sb, invocation, index);
            index++;
        }
    }

    private static void AppendSingleInterceptor(
        StringBuilder sb,
        GenAiCallSite invocation,
        int index)
    {
        var displayLocation = invocation.Location.GetDisplayLocation();
        var interceptAttribute = invocation.Location.GetInterceptsLocationAttributeSyntax();

        var methodName = $"Intercept_GenAi_{index}";
        var returnType = invocation.ReturnTypeName;
        var containingType = invocation.ContainingTypeName;
        var originalMethod = invocation.MethodName;

        var parameters = BuildParameterList(invocation, containingType);
        var arguments = BuildArgumentList(invocation);

        var modelArg = invocation.Model is not null
            ? $"\"{invocation.Model}\""
            : "null";

        // Use compile-time verified constants instead of literal strings
        var providerConst = GetProviderConstant(invocation.Provider);
        var operationConst = GetOperationConstant(invocation.Operation);

        var usageExtractor = GetUsageExtractor(invocation.Provider, invocation.Operation);

        if (invocation.IsAsync)
            sb.AppendLine(usageExtractor is not null
                ? $$"""
                            // Intercepted call at {{displayLocation}}
                            {{interceptAttribute}}
                            public static async {{returnType}} {{methodName}}({{parameters}})
                            {
                                return await GenAiInstrumentation.ExecuteAsync(
                                    {{providerConst}},
                                    {{operationConst}},
                                    {{modelArg}},
                                    async () => await @this.{{originalMethod}}({{arguments}}),
                                    {{usageExtractor}});
                            }

                    """
                : $$"""
                            // Intercepted call at {{displayLocation}}
                            {{interceptAttribute}}
                            public static async {{returnType}} {{methodName}}({{parameters}})
                            {
                                return await GenAiInstrumentation.ExecuteAsync(
                                    {{providerConst}},
                                    {{operationConst}},
                                    {{modelArg}},
                                    async () => await @this.{{originalMethod}}({{arguments}}));
                            }

                    """);
        else
            sb.AppendLine(usageExtractor is not null
                ? $$"""
                            // Intercepted call at {{displayLocation}}
                            {{interceptAttribute}}
                            public static {{returnType}} {{methodName}}({{parameters}})
                            {
                                return GenAiInstrumentation.Execute(
                                    {{providerConst}},
                                    {{operationConst}},
                                    {{modelArg}},
                                    () => @this.{{originalMethod}}({{arguments}}),
                                    {{usageExtractor}});
                            }

                    """
                : $$"""
                            // Intercepted call at {{displayLocation}}
                            {{interceptAttribute}}
                            public static {{returnType}} {{methodName}}({{parameters}})
                            {
                                return GenAiInstrumentation.Execute(
                                    {{providerConst}},
                                    {{operationConst}},
                                    {{modelArg}},
                                    () => @this.{{originalMethod}}({{arguments}}));
                            }

                    """);
    }

    private static void AppendStreamingInterceptor(
        StringBuilder sb,
        GenAiCallSite invocation,
        int index)
    {
        var displayLocation = invocation.Location.GetDisplayLocation();
        var interceptAttribute = invocation.Location.GetInterceptsLocationAttributeSyntax();

        var methodName = $"Intercept_GenAi_{index}";
        var returnType = invocation.ReturnTypeName;
        var containingType = invocation.ContainingTypeName;
        var originalMethod = invocation.MethodName;

        var parameters = BuildParameterList(invocation, containingType);
        var arguments = BuildArgumentList(invocation);

        var modelArg = invocation.Model is not null
            ? $"\"{invocation.Model}\""
            : "null";

        var providerConst = GetProviderConstant(invocation.Provider);
        var operationConst = GetOperationConstant(invocation.Operation);

        // Extract the element type from IAsyncEnumerable<T>
        var elementType = ExtractStreamingElementType(returnType);

        sb.AppendLine($$"""
                    // Intercepted streaming call at {{displayLocation}}
                    {{interceptAttribute}}
                    public static {{returnType}} {{methodName}}({{parameters}})
                    {
                        return GenAiInstrumentation.ExecuteStreamingAsync<{{elementType}}>(
                            {{providerConst}},
                            {{operationConst}},
                            {{modelArg}},
                            () => @this.{{originalMethod}}({{arguments}}));
                    }

            """);
    }

    /// <summary>
    ///     Extracts the element type from IAsyncEnumerable&lt;T&gt;.
    /// </summary>
    private static string ExtractStreamingElementType(string returnType)
    {
        // Expected format: System.Collections.Generic.IAsyncEnumerable<ElementType>
        var start = returnType.IndexOf('<');
        var end = returnType.LastIndexOf('>');

        if (start < 0 || end < 0 || end <= start)
            return "object"; // Fallback

        return returnType.Substring(start + 1, end - start - 1);
    }

    /// <summary>
    ///     Gets the usage extractor lambda for a provider/operation combination.
    ///     Uses ProviderRegistry as the SSOT.
    /// </summary>
    private static string? GetUsageExtractor(string provider, string operation)
    {
        var definition = ProviderRegistry.GenAiProviders
            .FirstOrDefault(p => p.ProviderId == provider);

        if (definition?.TokenUsage is null)
            return null;

        // Operations that support token usage extraction
        if (operation != "chat" && operation != "embeddings" && operation != "invoke_agent")
            return null;

        var usage = definition.TokenUsage;
        var outputTokens = operation == "embeddings" ? "0" : $"r.{usage.OutputProperty} ?? 0";
        var inputTokens = $"r.{usage.InputProperty} ?? 0";

        return $"static r => new TokenUsage({inputTokens}, {outputTokens})";
    }

    private static string BuildParameterList(GenAiCallSite invocation, string containingType)
    {
        var sb = new StringBuilder();
        sb.Append($"this global::{containingType} @this");

        for (var i = 0; i < invocation.ParameterTypes.Count; i++)
            sb.Append($", {EmitterHelpers.ToGlobalTypeName(invocation.ParameterTypes[i])} arg{i}");

        return sb.ToString();
    }


    private static string BuildArgumentList(GenAiCallSite invocation)
    {
        if (invocation.ParameterTypes.Count is 0)
            return string.Empty;

        var args = new string[invocation.ParameterTypes.Count];
        for (var i = 0; i < invocation.ParameterTypes.Count; i++)
            args[i] = $"arg{i}";

        return string.Join(", ", args);
    }


    /// <summary>
    ///     Maps a provider ID to its GenAiAttributes.Providers constant reference.
    ///     Falls back to literal string for unknown providers.
    /// </summary>
    private static string GetProviderConstant(string providerId)
    {
        return providerId switch
        {
            "openai" => "GenAiAttributes.Providers.OpenAi",
            "azure.ai.openai" => "GenAiAttributes.Providers.AzureOpenAi",
            "azure.ai.inference" => "GenAiAttributes.Providers.AzureAiInference",
            "anthropic" => "GenAiAttributes.Providers.Anthropic",
            "aws.bedrock" => "GenAiAttributes.Providers.AwsBedrock",
            "gcp.gemini" => "GenAiAttributes.Providers.GcpGemini",
            "gcp.vertex_ai" => "GenAiAttributes.Providers.GcpVertexAi",
            "cohere" => "GenAiAttributes.Providers.Cohere",
            "mistral_ai" => "GenAiAttributes.Providers.MistralAi",
            "groq" => "GenAiAttributes.Providers.Groq",
            "deepseek" => "GenAiAttributes.Providers.DeepSeek",
            "perplexity" => "GenAiAttributes.Providers.Perplexity",
            "x_ai" => "GenAiAttributes.Providers.XAi",
            "github_copilot" => "GenAiAttributes.Providers.GitHubCopilot",
            _ => $"\"{providerId}\"" // Fallback for custom providers
        };
    }

    /// <summary>
    ///     Maps an operation ID to its GenAiAttributes.Operations constant reference.
    ///     Falls back to literal string for unknown operations.
    /// </summary>
    private static string GetOperationConstant(string operationId)
    {
        return operationId switch
        {
            "chat" => "GenAiAttributes.Operations.Chat",
            "embeddings" => "GenAiAttributes.Operations.Embeddings",
            "text_completion" => "GenAiAttributes.Operations.TextCompletion",
            "create_agent" => "GenAiAttributes.Operations.CreateAgent",
            "invoke_agent" => "GenAiAttributes.Operations.InvokeAgent",
            "execute_tool" => "GenAiAttributes.Operations.ExecuteTool",
            "generate_content" => "GenAiAttributes.Operations.GenerateContent",
            _ => $"\"{operationId}\"" // Fallback for custom operations
        };
    }
}
