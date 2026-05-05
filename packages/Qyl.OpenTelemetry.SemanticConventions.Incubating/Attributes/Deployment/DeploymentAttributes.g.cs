

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Deployment;

public static class DeploymentAttributes
{
    [global::System.Obsolete("Replaced by deployment.environment.name.", false)]
    public const string Environment = "deployment.environment";

    public const string Id = "deployment.id";

    public const string Name = "deployment.name";

    public const string Status = "deployment.status";

    public static class StatusValues
    {
        public const string Failed = "failed";

        public const string Succeeded = "succeeded";
    }
}
