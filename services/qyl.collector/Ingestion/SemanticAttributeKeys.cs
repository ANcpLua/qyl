using ClientAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Client.ClientAttributes;
using StableCodeAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Code.CodeAttributes;
using StableDbAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Db.DbAttributes;
using StableDeploymentAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Deployment.DeploymentAttributes;
using StableErrorAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Error.ErrorAttributes;
using StableExceptionAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Exception.ExceptionAttributes;
using StableHttpAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Http.HttpAttributes;
using StableServerAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Server.ServerAttributes;
using StableServiceAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Service.ServiceAttributes;
using StableUrlAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Url.UrlAttributes;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using HostAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Host.HostAttributes;
using IncubatingDeploymentAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Deployment.DeploymentAttributes;
using MessagingAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Messaging.MessagingAttributes;
using McpAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Mcp.McpAttributes;
using OsAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Os.OsAttributes;
using SessionAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Session.SessionAttributes;
using UserAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.User.UserAttributes;
using EnduserAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Enduser.EnduserAttributes;

namespace Qyl.Collector.Ingestion;

internal static class SemanticAttributeKeys
{
    internal const string ClientAddress = ClientAttributes.Address;
    internal const string CodeColumnNumber = StableCodeAttributes.ColumnNumber;
    internal const string CodeFilePath = StableCodeAttributes.FilePath;
    internal const string CodeFunctionName = StableCodeAttributes.FunctionName;
    internal const string CodeLineNumber = StableCodeAttributes.LineNumber;
    internal const string CodeStacktrace = StableCodeAttributes.Stacktrace;
    internal const string DbOperationName = StableDbAttributes.OperationName;
    internal const string DbSystemName = StableDbAttributes.SystemName;
    internal const string DeploymentEnvironmentName = StableDeploymentAttributes.EnvironmentName;
    internal const string EnduserId = EnduserAttributes.Id;
    internal const string ErrorType = StableErrorAttributes.Type;
    internal const string ExceptionMessage = StableExceptionAttributes.Message;
    internal const string ExceptionStacktrace = StableExceptionAttributes.Stacktrace;
    internal const string ExceptionType = StableExceptionAttributes.Type;
    internal const string GenAiAgentId = GenAiAttributes.AgentId;
    internal const string GenAiAgentName = GenAiAttributes.AgentName;
    internal const string GenAiAgentDescription = GenAiAttributes.AgentDescription;
    internal const string GenAiAgentVersion = GenAiAttributes.AgentVersion;
    internal const string GenAiConversationId = GenAiAttributes.ConversationId;
    internal const string GenAiDataSourceId = GenAiAttributes.DataSourceId;
    internal const string GenAiInputMessages = GenAiAttributes.InputMessages;
    internal const string GenAiOperationName = GenAiAttributes.OperationName;
    internal const string GenAiProviderName = GenAiAttributes.ProviderName;
    internal const string GenAiRequestModel = GenAiAttributes.RequestModel;
    internal const string GenAiRequestTemperature = GenAiAttributes.RequestTemperature;
    internal const string GenAiResponseFinishReasons = GenAiAttributes.ResponseFinishReasons;
    internal const string GenAiResponseModel = GenAiAttributes.ResponseModel;
    internal const string GenAiToolCallId = GenAiAttributes.ToolCallId;
    internal const string GenAiToolName = GenAiAttributes.ToolName;
    internal const string GenAiUsageInputTokens = GenAiAttributes.UsageInputTokens;
    internal const string GenAiUsageOutputTokens = GenAiAttributes.UsageOutputTokens;
    internal const string HostArch = HostAttributes.Arch;
    internal const string HttpRequestMethod = StableHttpAttributes.RequestMethod;
    internal const string HttpRoute = StableHttpAttributes.Route;
    internal const string MessagingSystem = MessagingAttributes.System;
    internal const string McpSessionId = McpAttributes.SessionId;
    internal const string OsType = OsAttributes.Type;
    internal const string ServiceInstanceId = StableServiceAttributes.InstanceId;
    internal const string ServiceName = StableServiceAttributes.Name;
    internal const string ServiceNamespace = StableServiceAttributes.Namespace;
    internal const string ServiceVersion = StableServiceAttributes.Version;
    internal const string SessionId = SessionAttributes.Id;
    internal const string ServerAddress = StableServerAttributes.Address;
    internal const string UrlFull = StableUrlAttributes.Full;
    internal const string UrlPath = StableUrlAttributes.Path;
    internal const string UserId = UserAttributes.Id;

    // Collector-local keys without an upstream semconv constant in the current package.
    internal const string GenAiCostUsd = "gen_ai.usage.cost";
    internal const string GenAiErrorType = "gen_ai.error.type";
    internal const string MeterName = "meter.name";

    internal const string ClaudeCodePrefix = "claude_code.";
    internal const string McpPrefix = "mcp.";
    internal const string QylCapabilityPrefix = "qyl.capability.";

    internal static readonly string[] SessionCorrelationKeys =
    [
        GenAiConversationId,
        McpSessionId,
        SessionId
    ];

    internal static string? GetFirstValueOrDefault(
        this IReadOnlyDictionary<string, string> attributes,
        IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            if (attributes.GetValueOrDefault(key) is { } value)
                return value;
        }

        return null;
    }

    internal static bool IsAny(
        this string key,
        IReadOnlyList<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (key.EqualsOrdinal(candidate))
                return true;
        }

        return false;
    }
}
