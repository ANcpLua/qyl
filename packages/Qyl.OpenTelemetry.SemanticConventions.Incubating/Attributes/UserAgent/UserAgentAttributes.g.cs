

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.UserAgent;

public static class UserAgentAttributes
{
    public const string Name = "user_agent.name";

    public const string OsName = "user_agent.os.name";

    public const string OsVersion = "user_agent.os.version";

    public const string SyntheticType = "user_agent.synthetic.type";

    public static class SyntheticTypeValues
    {
        public const string Bot = "bot";

        public const string Test = "test";
    }

    public const string Version = "user_agent.version";
}
