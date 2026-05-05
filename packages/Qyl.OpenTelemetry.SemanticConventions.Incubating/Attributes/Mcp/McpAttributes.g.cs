

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Mcp;

public static class McpAttributes
{
    public const string MethodName = "mcp.method.name";

    public static class MethodNameValues
    {
        public const string CompletionComplete = "completion/complete";

        public const string ElicitationCreate = "elicitation/create";

        public const string Initialize = "initialize";

        public const string LoggingSetLevel = "logging/setLevel";

        public const string NotificationsCancelled = "notifications/cancelled";

        public const string NotificationsInitialized = "notifications/initialized";

        public const string NotificationsMessage = "notifications/message";

        public const string NotificationsProgress = "notifications/progress";

        public const string NotificationsPromptsListChanged = "notifications/prompts/list_changed";

        public const string NotificationsResourcesListChanged = "notifications/resources/list_changed";

        public const string NotificationsResourcesUpdated = "notifications/resources/updated";

        public const string NotificationsRootsListChanged = "notifications/roots/list_changed";

        public const string NotificationsToolsListChanged = "notifications/tools/list_changed";

        public const string Ping = "ping";

        public const string PromptsGet = "prompts/get";

        public const string PromptsList = "prompts/list";

        public const string ResourcesList = "resources/list";

        public const string ResourcesRead = "resources/read";

        public const string ResourcesSubscribe = "resources/subscribe";

        public const string ResourcesTemplatesList = "resources/templates/list";

        public const string ResourcesUnsubscribe = "resources/unsubscribe";

        public const string RootsList = "roots/list";

        public const string SamplingCreateMessage = "sampling/createMessage";

        public const string ToolsCall = "tools/call";

        public const string ToolsList = "tools/list";
    }

    public const string ProtocolVersion = "mcp.protocol.version";

    public const string ResourceUri = "mcp.resource.uri";

    public const string SessionId = "mcp.session.id";
}
