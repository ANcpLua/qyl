using System.Collections.Immutable;
using ANcpLua.Roslyn.Utilities;
using Microsoft.CodeAnalysis.CSharp;
using Qyl.ServiceDefaults.Generator.Models;

namespace Qyl.ServiceDefaults.Generator.Emitters;

/// <summary>
///     Emits interceptor source code for agent invocations detected by <see cref="Analyzers.AgentCallSiteAnalyzer" />.
/// </summary>
/// <remarks>
///     Generated span hierarchy:
///     <code>
///     gen_ai.agent.invoke (root span)
///       ├── gen_ai.agent.name
///       ├── gen_ai.operation.name = invoke_agent
///       └── error handling (never throws, just stops tracing)
///     </code>
/// </remarks>
internal static class AgentInterceptorEmitter
{
    private const string ActivitySourceName = "qyl.agent";

    /// <summary>
    ///     Emits the interceptor source code for all agent invocations.
    /// </summary>
    public static string Emit(ImmutableArray<AgentCallSite> invocations)
    {
        if (invocations.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder();

        EmitterHelpers.AppendFileHeader(sb, true);
        AppendUsings(sb);
        EmitterHelpers.AppendInterceptsLocationAttribute(sb);
        AppendActivitySourceClass(sb);
        AppendClassOpen(sb);
        AppendInterceptorMethods(sb, invocations);
        EmitterHelpers.AppendClassClose(sb);

        return sb.ToString();
    }

    private static void AppendUsings(StringBuilder sb)
    {
        sb.AppendLine("using System.Diagnostics;");
        sb.AppendLine("using qyl.protocol.Attributes;");
        sb.AppendLine();
    }

    private static void AppendActivitySourceClass(StringBuilder sb)
    {
        sb.AppendLine($$"""
                        namespace Qyl.ServiceDefaults.Generator
                        {
                            file static class AgentActivitySources
                            {
                                internal static readonly global::System.Diagnostics.ActivitySource Source = new("{{ActivitySourceName}}");
                            }
                        }
                        """);
        sb.AppendLine();
    }

    private static void AppendClassOpen(StringBuilder sb) =>
        sb.AppendLine("""
                      namespace Qyl.ServiceDefaults.Generator
                      {
                          file static class AgentInterceptors
                          {
                      """);

    private static void AppendInterceptorMethods(
        StringBuilder sb,
        ImmutableArray<AgentCallSite> invocations)
    {
        var orderedInvocations = invocations
            .OrderBy(static i => i.SortKey, StringComparer.Ordinal);

        var index = 0;
        foreach (var invocation in orderedInvocations)
        {
            AppendSingleInterceptor(sb, invocation, index);
            index++;
        }
    }

    private static void AppendSingleInterceptor(
        StringBuilder sb,
        AgentCallSite invocation,
        int index)
    {
        var displayLocation = invocation.Location.GetDisplayLocation();
        var interceptAttribute = invocation.Location.GetInterceptsLocationAttributeSyntax();

        var methodName = $"Intercept_Agent_{index}";
        var returnType = invocation.ReturnTypeName;
        var containingType = invocation.ContainingTypeName;
        var originalMethod = invocation.MethodName;

        var parameters = EmitterHelpers.BuildParameterList(
            containingType, invocation.ParameterTypes, invocation.ParameterNames);
        var arguments = EmitterHelpers.BuildArgumentList(invocation.ParameterNames);

        var spanName = GetSpanName(invocation);
        var agentNameLiteral = invocation.AgentName is not null
            ? $"\"{EscapeString(invocation.AgentName)}\""
            : "null";

        var operationConst = GetOperationConstant(invocation.Kind);

        if (invocation.IsAsync)
        {
            EmitAsyncInterceptor(sb, methodName, returnType, parameters, arguments,
                displayLocation, interceptAttribute, originalMethod, spanName,
                agentNameLiteral, operationConst);
        }
        else
        {
            EmitSyncInterceptor(sb, methodName, returnType, parameters, arguments,
                displayLocation, interceptAttribute, originalMethod, spanName,
                agentNameLiteral, operationConst);
        }
    }

    private static void EmitAsyncInterceptor(
        StringBuilder sb,
        string methodName,
        string returnType,
        string parameters,
        string arguments,
        string displayLocation,
        string interceptAttribute,
        string originalMethod,
        string spanName,
        string agentNameLiteral,
        string operationConst)
    {
        var hasReturnValue = !returnType.EndsWithOrdinal("Task") &&
                             !returnType.EndsWithOrdinal("ValueTask");

        sb.AppendLine($$"""
                                // Intercepted agent call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static async {{returnType}} {{methodName}}({{parameters}})
                                {
                                    using var activity = AgentActivitySources.Source.StartActivity(
                                        "{{spanName}}",
                                        global::System.Diagnostics.ActivityKind.Client);

                                    if (activity is not null)
                                    {
                                        activity.SetTag(GenAiAttributes.OperationName, {{operationConst}});
                                        activity.SetTag(GenAiAttributes.ProviderName, GenAiAttributes.Providers.MicrosoftAgents);
                                        if ({{agentNameLiteral}} is not null)
                                            activity.SetTag(GenAiAttributes.AgentName, {{agentNameLiteral}});
                                    }

                        """);

        sb.AppendLine(hasReturnValue
            ? $$"""
                            try
                            {
                                var result = await @this.{{originalMethod}}({{arguments}});
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                                return result;
                            }
                            catch (global::System.Exception ex)
                            {
                                if (activity is not null)
                                {
                                    activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                    activity.SetTag(GenAiAttributes.ErrorType, ex.GetType().Name);
                                    activity.AddEvent(new global::System.Diagnostics.ActivityEvent("exception",
                                        tags: new global::System.Diagnostics.ActivityTagsCollection
                                        {
                                            { GenAiAttributes.ExceptionType, ex.GetType().FullName },
                                            { GenAiAttributes.ExceptionMessage, ex.Message }
                                        }));
                                }
                                throw;
                            }
                        }

                """
            : $$"""
                            try
                            {
                                await @this.{{originalMethod}}({{arguments}});
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                            }
                            catch (global::System.Exception ex)
                            {
                                if (activity is not null)
                                {
                                    activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                    activity.SetTag(GenAiAttributes.ErrorType, ex.GetType().Name);
                                    activity.AddEvent(new global::System.Diagnostics.ActivityEvent("exception",
                                        tags: new global::System.Diagnostics.ActivityTagsCollection
                                        {
                                            { GenAiAttributes.ExceptionType, ex.GetType().FullName },
                                            { GenAiAttributes.ExceptionMessage, ex.Message }
                                        }));
                                }
                                throw;
                            }
                        }

                """);
    }

    private static void EmitSyncInterceptor(
        StringBuilder sb,
        string methodName,
        string returnType,
        string parameters,
        string arguments,
        string displayLocation,
        string interceptAttribute,
        string originalMethod,
        string spanName,
        string agentNameLiteral,
        string operationConst)
    {
        var hasReturnValue = returnType != "void";

        sb.AppendLine($$"""
                                // Intercepted agent call at {{displayLocation}}
                                {{interceptAttribute}}
                                public static {{returnType}} {{methodName}}({{parameters}})
                                {
                                    using var activity = AgentActivitySources.Source.StartActivity(
                                        "{{spanName}}",
                                        global::System.Diagnostics.ActivityKind.Client);

                                    if (activity is not null)
                                    {
                                        activity.SetTag(GenAiAttributes.OperationName, {{operationConst}});
                                        activity.SetTag(GenAiAttributes.ProviderName, GenAiAttributes.Providers.MicrosoftAgents);
                                        if ({{agentNameLiteral}} is not null)
                                            activity.SetTag(GenAiAttributes.AgentName, {{agentNameLiteral}});
                                    }

                        """);

        sb.AppendLine(hasReturnValue
            ? $$"""
                            try
                            {
                                var result = @this.{{originalMethod}}({{arguments}});
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                                return result;
                            }
                            catch (global::System.Exception ex)
                            {
                                if (activity is not null)
                                {
                                    activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                    activity.SetTag(GenAiAttributes.ErrorType, ex.GetType().Name);
                                    activity.AddEvent(new global::System.Diagnostics.ActivityEvent("exception",
                                        tags: new global::System.Diagnostics.ActivityTagsCollection
                                        {
                                            { GenAiAttributes.ExceptionType, ex.GetType().FullName },
                                            { GenAiAttributes.ExceptionMessage, ex.Message }
                                        }));
                                }
                                throw;
                            }
                        }

                """
            : $$"""
                            try
                            {
                                @this.{{originalMethod}}({{arguments}});
                                activity?.SetStatus(global::System.Diagnostics.ActivityStatusCode.Ok);
                            }
                            catch (global::System.Exception ex)
                            {
                                if (activity is not null)
                                {
                                    activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error, ex.Message);
                                    activity.SetTag(GenAiAttributes.ErrorType, ex.GetType().Name);
                                    activity.AddEvent(new global::System.Diagnostics.ActivityEvent("exception",
                                        tags: new global::System.Diagnostics.ActivityTagsCollection
                                        {
                                            { GenAiAttributes.ExceptionType, ex.GetType().FullName },
                                            { GenAiAttributes.ExceptionMessage, ex.Message }
                                        }));
                                }
                                throw;
                            }
                        }

                """);
    }

    private static string GetSpanName(AgentCallSite invocation) =>
        invocation.Kind switch
        {
            AgentCallKind.InvokeAsync => invocation.AgentName is not null
                ? $"invoke_agent {EscapeString(invocation.AgentName)}"
                : "invoke_agent",
            AgentCallKind.BuilderRegistration => invocation.AgentName is not null
                ? $"create_agent {EscapeString(invocation.AgentName)}"
                : "create_agent",
            AgentCallKind.AgentTracedMethod => invocation.AgentName is not null
                ? $"invoke_agent {EscapeString(invocation.AgentName)}"
                : "invoke_agent",
            _ => "invoke_agent"
        };

    private static string GetOperationConstant(AgentCallKind kind) =>
        kind switch
        {
            AgentCallKind.InvokeAsync => "GenAiAttributes.Operations.InvokeAgent",
            AgentCallKind.BuilderRegistration => "GenAiAttributes.Operations.CreateAgent",
            AgentCallKind.AgentTracedMethod => "GenAiAttributes.Operations.InvokeAgent",
            _ => "GenAiAttributes.Operations.InvokeAgent"
        };

    private static string EscapeString(string value) => value.Replace("\\", @"\\").Replace("\"", "\\\"");
}
