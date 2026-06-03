using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

    private static readonly FrozenSet<string> s_qylResourceAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        QylAttr.Capability.Id,
        QylAttr.Capability.Kind);

    private static readonly FrozenSet<string> s_spanAttributeAllowList = BuildAttributeSet(
        typeof(DbAttributes),
        typeof(ErrorAttributes),
        typeof(ExceptionAttributes),
        typeof(GenAiAttributes),
        typeof(HttpAttributes),
        typeof(MessagingAttributes),
        typeof(OtelAttributes),
        typeof(ProfileAttributes),
        typeof(ServerAttributes));

    private static readonly FrozenSet<string> s_logAttributeAllowList = BuildAttributeSet(
        typeof(ErrorAttributes),
        typeof(ExceptionAttributes),
        typeof(GenAiAttributes),
        typeof(HttpAttributes),
        typeof(MessagingAttributes),
        typeof(OtelAttributes),
        typeof(ServerAttributes));

    private static readonly FrozenSet<string> s_profileAttributeAllowList = BuildAttributeSet(
        typeof(ErrorAttributes),
        typeof(GenAiAttributes),
        typeof(OtelAttributes),
        typeof(ProfileAttributes));

    private static readonly FrozenSet<string> s_resourceAttributeAllowList = BuildAttributeSet(
        typeof(DeploymentAttributes),
        typeof(HostAttributes),
        typeof(OsAttributes),
        typeof(ServiceAttributes));

    private static readonly FrozenSet<string> s_deniedExactKeys = BuildDeniedSet(
        AttributesFrom(typeof(EnduserAttributes)).Concat(AttributesFrom(typeof(UserAttributes))),
        CodeAttributes.FilePath,
        CodeAttributes.Stacktrace,
        DbAttributes.QueryText,
        ExceptionAttributes.Message,
        ExceptionAttributes.Stacktrace,
        GenAiAttributes.AgentDescription,
        GenAiAttributes.AgentId,
        GenAiAttributes.AgentName,
        GenAiAttributes.ConversationId,
        GenAiAttributes.DataSourceId,
        GenAiAttributes.EvaluationExplanation,
        GenAiAttributes.EvaluationName,
        GenAiAttributes.InputMessages,
        GenAiAttributes.OutputMessages,
        GenAiAttributes.PromptName,
        GenAiAttributes.ResponseId,
        GenAiAttributes.RetrievalDocuments,
        GenAiAttributes.RetrievalQueryText,
        GenAiAttributes.SystemInstructions,
        GenAiAttributes.ToolCallArguments,
        HttpAttributes.RequestHeader,
        HttpAttributes.ResponseHeader,
        GenAiAttributes.ToolCallId,
        GenAiAttributes.ToolCallResult,
        GenAiAttributes.ToolDefinitions,
        GenAiAttributes.ToolDescription,
        GenAiAttributes.WorkflowName,
        McpAttributes.SessionId,
        ServiceAttributes.InstanceId,
        SessionAttributes.Id,
        SessionAttributes.PreviousId,
        UrlAttributes.Full,
        UrlAttributes.Query);

    private static readonly string[] s_deniedKeyTokens =
    [
        "access_token",
        "api-key",
        "api_key",
        "apikey",
        "authorization",
        "body",
        "completion",
        "cookie",
        "credential",
        "definition",
        "document",
        "fingerprint",
        "id_token",
        "instruction",
        "jwt",
        "message",
        "password",
        "private_key",
        "prompt",
        "query",
        "refresh_token",
        "result",
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
        (s_resourceAttributeAllowList.Contains(key) || s_qylResourceAttributeAllowList.Contains(key));

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

    private static FrozenSet<string> BuildAttributeSet(params Type[] attributeTypes) =>
        attributeTypes
            .SelectMany(AttributesFrom)
            .ToFrozenSet(StringComparer.Ordinal);

    private static IEnumerable<string> AttributesFrom(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] Type attributeType) =>
        attributeType
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(static field => field is { IsLiteral: true, IsInitOnly: false } &&
                                   field.FieldType == typeof(string))
            .Select(static field => (string)field.GetRawConstantValue()!);

    private static FrozenSet<string> BuildDeniedSet(
        IEnumerable<string> dynamicKeys,
        params string[] exactKeys) =>
        dynamicKeys
            .Concat(exactKeys)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);
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
