

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Attributes.Deployment;

public static class DeploymentAttributes
{
    public const string EnvironmentName = "deployment.environment.name";

    public static class EnvironmentNameValues
    {
        public const string Development = "development";

        public const string Production = "production";

        public const string Staging = "staging";

        public const string Test = "test";
    }
}
