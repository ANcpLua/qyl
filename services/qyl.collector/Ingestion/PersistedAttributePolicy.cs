namespace Qyl.Collector.Ingestion;

internal static class PersistedAttributePolicy
{
    private const string BaggagePrefix = "baggage.";
    private const string CodexPrefix = "codex.";

    private static readonly FrozenSet<string> s_spanAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        SemanticAttributeKeys.DbOperationName,
        SemanticAttributeKeys.DbSystemName,
        SemanticAttributeKeys.ErrorType,
        SemanticAttributeKeys.ExceptionType,
        SemanticAttributeKeys.GenAiOperationName,
        SemanticAttributeKeys.GenAiProviderName,
        SemanticAttributeKeys.GenAiRequestModel,
        SemanticAttributeKeys.GenAiResponseFinishReasons,
        SemanticAttributeKeys.GenAiResponseModel,
        SemanticAttributeKeys.GenAiToolName,
        SemanticAttributeKeys.HttpRequestMethod,
        SemanticAttributeKeys.HttpRoute,
        SemanticAttributeKeys.MessagingSystem,
        SemanticAttributeKeys.OtelScopeName,
        SemanticAttributeKeys.ProfileFrameType,
        SemanticAttributeKeys.ServerAddress,
        SemanticAttributeKeys.UrlPath);

    private static readonly FrozenSet<string> s_resourceAttributeAllowList = FrozenSet.Create(
        StringComparer.Ordinal,
        SemanticAttributeKeys.DeploymentEnvironmentName,
        SemanticAttributeKeys.HostArch,
        SemanticAttributeKeys.OsType,
        SemanticAttributeKeys.ServiceName,
        SemanticAttributeKeys.ServiceNamespace,
        SemanticAttributeKeys.ServiceVersion);

    private static readonly FrozenSet<string> s_deniedExactKeys = FrozenSet.Create(
        StringComparer.Ordinal,
        SemanticAttributeKeys.CodeFilePath,
        SemanticAttributeKeys.CodeStacktrace,
        SemanticAttributeKeys.EnduserId,
        SemanticAttributeKeys.ExceptionMessage,
        SemanticAttributeKeys.ExceptionStacktrace,
        SemanticAttributeKeys.GenAiAgentDescription,
        SemanticAttributeKeys.GenAiAgentId,
        SemanticAttributeKeys.GenAiConversationId,
        SemanticAttributeKeys.GenAiDataSourceId,
        SemanticAttributeKeys.GenAiInputMessages,
        SemanticAttributeKeys.McpSessionId,
        SemanticAttributeKeys.ServiceInstanceId,
        SemanticAttributeKeys.SessionId,
        SemanticAttributeKeys.UrlFull,
        SemanticAttributeKeys.UserId);

    internal static string? SerializeSpanAttributes(
        IReadOnlyDictionary<string, string> attributes,
        bool keepCodexForPendingTransform = false) =>
        Serialize(attributes, keepCodexForPendingTransform
            ? ShouldPersistSpanAttributeForPendingTransform
            : ShouldPersistSpanAttribute);

    internal static string? SerializeLogAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistSpanAttribute);

    internal static string? SerializeProfileAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistSpanAttribute);

    internal static string? SerializeResourceAttributes(IReadOnlyDictionary<string, string> attributes) =>
        Serialize(attributes, ShouldPersistResourceAttribute);

    internal static bool HasCodexAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        foreach (var key in attributes.Keys)
            if (key.StartsWithOrdinal(CodexPrefix))
                return true;

        return false;
    }

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

    private static bool ShouldPersistSpanAttribute(string key) =>
        ShouldPersistSpanAttribute(key, keepCodexForPendingTransform: false);

    private static bool ShouldPersistSpanAttributeForPendingTransform(string key) =>
        ShouldPersistSpanAttribute(key, keepCodexForPendingTransform: true);

    private static bool ShouldPersistSpanAttribute(string key, bool keepCodexForPendingTransform)
    {
        if (keepCodexForPendingTransform && key.StartsWithOrdinal(CodexPrefix))
            return true;

        if (IsDenied(key))
            return false;

        return s_spanAttributeAllowList.Contains(key);
    }

    private static bool ShouldPersistResourceAttribute(string key)
    {
        if (IsDenied(key))
            return false;

        return s_resourceAttributeAllowList.Contains(key) ||
               key.StartsWithOrdinal(SemanticAttributeKeys.QylCapabilityPrefix);
    }

    private static bool IsDenied(string key) =>
        s_deniedExactKeys.Contains(key) ||
        key.StartsWithOrdinal(BaggagePrefix) ||
        key.StartsWithOrdinal(CodexPrefix) ||
        key.ContainsOrdinal("password") ||
        key.ContainsOrdinal("secret") ||
        key.ContainsOrdinal("authorization") ||
        key.ContainsOrdinal("cookie") ||
        key.ContainsOrdinal("api_key");
}
