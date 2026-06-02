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

internal static class PersistedAttributePolicy
{
    private const string BaggagePrefix = "baggage.";

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

    private static readonly FrozenSet<string> s_resourceAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        DeploymentAttributes.EnvironmentName,
        HostAttributes.Arch,
        OsAttributes.Type,
        ServiceAttributes.Name,
        ServiceAttributes.Namespace,
        ServiceAttributes.Version);

    private static readonly FrozenSet<string> s_deniedExactKeys = FrozenSet.Create(
        StringComparer.Ordinal,
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
        McpAttributes.SessionId,
        ServiceAttributes.InstanceId,
        SessionAttributes.Id,
        UrlAttributes.Full,
        UserAttributes.Id);

    internal static string? SerializeSpanAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistSpanAttribute);

    internal static string? SerializeLogAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistSpanAttribute);

    internal static string? SerializeProfileAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistSpanAttribute);

    internal static string? SerializeResourceAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistResourceAttribute);

    private static string? Serialize(
        IReadOnlyDictionary<string, string> attributes,
        Func<string, bool> shouldPersist)
    {
        Dictionary<string, string>? persisted = null;

        foreach (var (key, value) in attributes)
        {
            if (!shouldPersist(key))
                continue;

            persisted ??= new Dictionary<string, string>(StringComparer.Ordinal);
            persisted[key] = value;
        }

        return persisted is null
            ? null
            : JsonSerializer.Serialize(persisted, QylSerializerContext.Default.DictionaryStringString);
    }

    private static bool ShouldPersistSpanAttribute(string key)
    {
        if (IsDenied(key))
            return false;

        return s_spanAttributeAllowList.Contains(key);
    }

    private static bool ShouldPersistResourceAttribute(string key)
    {
        if (IsDenied(key))
            return false;

        return s_resourceAttributeAllowList.Contains(key) ||
               key.StartsWithOrdinal(AttributeKeySets.QylCapabilityPrefix);
    }

    private static bool IsDenied(string key) =>
        s_deniedExactKeys.Contains(key) ||
        key.StartsWithOrdinal(BaggagePrefix) ||
        key.ContainsOrdinal("password") ||
        key.ContainsOrdinal("secret") ||
        key.ContainsOrdinal("authorization") ||
        key.ContainsOrdinal("cookie") ||
        key.ContainsOrdinal("api_key");
}
