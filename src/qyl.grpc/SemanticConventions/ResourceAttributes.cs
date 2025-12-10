namespace qyl.grpc.SemanticConventions;

public static class ResourceAttributes
{
    public static class Service
    {
        public const string Name = "service.name";
        public const string Version = "service.version";
        public const string Namespace = "service.namespace";
        public const string InstanceId = "service.instance.id";
    }

    public static class Deployment
    {
        public const string Environment = "deployment.environment";
        public const string EnvironmentName = "deployment.environment.name";
    }

    public static class Host
    {
        public const string Name = "host.name";
        public const string Type = "host.type";
        public const string Arch = "host.arch";
    }

    public static class Process
    {
        public const string Pid = "process.pid";
        public const string ExecutableName = "process.executable.name";
        public const string ExecutablePath = "process.executable.path";
        public const string RuntimeName = "process.runtime.name";
        public const string RuntimeVersion = "process.runtime.version";
    }

    public static class Telemetry
    {
        public const string SdkName = "telemetry.sdk.name";
        public const string SdkLanguage = "telemetry.sdk.language";
        public const string SdkVersion = "telemetry.sdk.version";
        public const string AutoVersion = "telemetry.auto.version";
    }

    public static class Container
    {
        public const string Id = "container.id";
        public const string Name = "container.name";
        public const string ImageName = "container.image.name";
        public const string ImageTag = "container.image.tag";
    }

    public static class K8S
    {
        public const string ClusterName = "k8s.cluster.name";
        public const string NamespaceName = "k8s.namespace.name";
        public const string PodName = "k8s.pod.name";
        public const string DeploymentName = "k8s.deployment.name";
    }

    public static class Cloud
    {
        public const string Provider = "cloud.provider";
        public const string Region = "cloud.region";
        public const string AvailabilityZone = "cloud.availability_zone";
        public const string AccountId = "cloud.account.id";
    }
}
