using CodeAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Code.CodeAttributes;
using DbAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Db.DbAttributes;
using DeploymentAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Deployment.DeploymentAttributes;
using EnduserAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Enduser.EnduserAttributes;
using ErrorAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Error.ErrorAttributes;
using ExceptionAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Exception.ExceptionAttributes;
using GenAiAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.GenAi.GenAiAttributes;
using HostAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Host.HostAttributes;
using HttpAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Http.HttpAttributes;
using McpAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Mcp.McpAttributes;
using MessagingAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Messaging.MessagingAttributes;
using OsAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Os.OsAttributes;
using OtelAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Otel.OtelAttributes;
using ProfileAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Profile.ProfileAttributes;
using ServerAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Server.ServerAttributes;
using ServiceAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Service.ServiceAttributes;
using SessionAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Session.SessionAttributes;
using UrlAttributes = Qyl.OpenTelemetry.SemanticConventions.Attributes.Url.UrlAttributes;
using UserAttributes = Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.User.UserAttributes;

namespace Qyl.Collector.Ingestion;

internal static class AttributeKeySets
{
    internal const string BaggagePrefix = "baggage.";

    internal static readonly FrozenSet<string> SessionCorrelation = FrozenSet.Create(
        StringComparer.Ordinal,
        GenAiAttributes.ConversationId,
        McpAttributes.SessionId,
        SessionAttributes.Id);

    internal static readonly string QylCapabilityPrefix = AttributeKeyPrefix.Of(QylAttr.Capability.Id);

    private static readonly FrozenSet<string> s_spanAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        DbAttributes.OperationName,
        DbAttributes.SystemName,
        ErrorAttributes.Type,
        ExceptionAttributes.Type,
        GenAiAttributes.OperationName,
        GenAiAttributes.ProviderName,
        GenAiAttributes.RequestModel,
        GenAiAttributes.ResponseFinishReasons,
        GenAiAttributes.ResponseModel,
        GenAiAttributes.ToolName,
        HttpAttributes.RequestMethod,
        HttpAttributes.Route,
        MessagingAttributes.System,
        OtelAttributes.ScopeName,
        ProfileAttributes.FrameType,
        ServerAttributes.Address);

    private static readonly FrozenSet<string> s_logAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        ErrorAttributes.Type,
        ExceptionAttributes.Type,
        GenAiAttributes.OperationName,
        GenAiAttributes.ProviderName,
        GenAiAttributes.RequestModel,
        GenAiAttributes.ResponseFinishReasons,
        GenAiAttributes.ResponseModel,
        GenAiAttributes.ToolName,
        HttpAttributes.RequestMethod,
        HttpAttributes.Route,
        MessagingAttributes.System,
        OtelAttributes.ScopeName,
        ServerAttributes.Address);

    private static readonly FrozenSet<string> s_profileAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        ErrorAttributes.Type,
        GenAiAttributes.OperationName,
        GenAiAttributes.ProviderName,
        GenAiAttributes.RequestModel,
        GenAiAttributes.ResponseModel,
        OtelAttributes.ScopeName,
        ProfileAttributes.FrameType);

    private static readonly FrozenSet<string> s_resourceAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        DeploymentAttributes.EnvironmentName,
        HostAttributes.Arch,
        OsAttributes.Type,
        ServiceAttributes.Name,
        ServiceAttributes.Namespace,
        ServiceAttributes.Version);

    private static readonly FrozenSet<string> s_deniedExactKeys = FrozenSet.Create(
        StringComparer.OrdinalIgnoreCase,
        CodeAttributes.FilePath,
        CodeAttributes.Stacktrace,
        EnduserAttributes.Id,
        ExceptionAttributes.Message,
        ExceptionAttributes.Stacktrace,
        GenAiAttributes.AgentDescription,
        GenAiAttributes.AgentId,
        GenAiAttributes.ConversationId,
        GenAiAttributes.DataSourceId,
        GenAiAttributes.InputMessages,
        GenAiAttributes.ToolCallId,
        McpAttributes.SessionId,
        ServiceAttributes.InstanceId,
        SessionAttributes.Id,
        UrlAttributes.Full,
        UserAttributes.Id);

    private static readonly string[] s_deniedKeyTokens =
    [
        "access_token",
        "api-key",
        "api_key",
        "apikey",
        "authorization",
        "cookie",
        "credential",
        "id_token",
        "jwt",
        "password",
        "private_key",
        "refresh_token",
        "secret",
        "set-cookie",
        "token"
    ];

    internal static bool ShouldPersistSpanAttribute(string key) =>
        !IsDenied(key) && s_spanAttributeAllowList.Contains(key);

    internal static bool ShouldPersistLogAttribute(string key) =>
        !IsDenied(key) && s_logAttributeAllowList.Contains(key);

    internal static bool ShouldPersistProfileAttribute(string key) =>
        !IsDenied(key) && s_profileAttributeAllowList.Contains(key);

    internal static bool ShouldPersistResourceAttribute(string key) =>
        !IsDenied(key) &&
        (s_resourceAttributeAllowList.Contains(key) || key.StartsWithOrdinal(QylCapabilityPrefix));

    private static bool IsDenied(string key)
    {
        if (s_deniedExactKeys.Contains(key) ||
            key.StartsWith(BaggagePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var token in s_deniedKeyTokens)
        {
            if (key.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

internal static class AttributeLookupExtensions
{
    internal static string? GetFirstValueOrDefault(
        this IReadOnlyDictionary<string, string> attributes,
        IEnumerable<string> keys)
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
        FrozenSet<string> candidates) =>
        candidates.Contains(key);
}

internal static class AttributeKeyPrefix
{
    internal static string Of(string key)
    {
        var lastDot = key.LastIndexOf('.');
        return lastDot < 0 ? key : key[..(lastDot + 1)];
    }
}
