

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Gcp;

public static class GcpAttributes
{
    public const string ApphubApplicationContainer = "gcp.apphub.application.container";

    public const string ApphubApplicationId = "gcp.apphub.application.id";

    public const string ApphubApplicationLocation = "gcp.apphub.application.location";

    public const string ApphubServiceCriticalityType = "gcp.apphub.service.criticality_type";

    public static class ApphubServiceCriticalityTypeValues
    {
        public const string High = "HIGH";

        public const string Low = "LOW";

        public const string Medium = "MEDIUM";

        public const string MissionCritical = "MISSION_CRITICAL";
    }

    public const string ApphubServiceEnvironmentType = "gcp.apphub.service.environment_type";

    public static class ApphubServiceEnvironmentTypeValues
    {
        public const string Development = "DEVELOPMENT";

        public const string Production = "PRODUCTION";

        public const string Staging = "STAGING";

        public const string Test = "TEST";
    }

    public const string ApphubServiceId = "gcp.apphub.service.id";

    public const string ApphubWorkloadCriticalityType = "gcp.apphub.workload.criticality_type";

    public static class ApphubWorkloadCriticalityTypeValues
    {
        public const string High = "HIGH";

        public const string Low = "LOW";

        public const string Medium = "MEDIUM";

        public const string MissionCritical = "MISSION_CRITICAL";
    }

    public const string ApphubWorkloadEnvironmentType = "gcp.apphub.workload.environment_type";

    public static class ApphubWorkloadEnvironmentTypeValues
    {
        public const string Development = "DEVELOPMENT";

        public const string Production = "PRODUCTION";

        public const string Staging = "STAGING";

        public const string Test = "TEST";
    }

    public const string ApphubWorkloadId = "gcp.apphub.workload.id";

    public const string ApphubDestinationApplicationContainer = "gcp.apphub_destination.application.container";

    public const string ApphubDestinationApplicationId = "gcp.apphub_destination.application.id";

    public const string ApphubDestinationApplicationLocation = "gcp.apphub_destination.application.location";

    public const string ApphubDestinationServiceCriticalityType = "gcp.apphub_destination.service.criticality_type";

    public static class ApphubDestinationServiceCriticalityTypeValues
    {
        public const string High = "HIGH";

        public const string Low = "LOW";

        public const string Medium = "MEDIUM";

        public const string MissionCritical = "MISSION_CRITICAL";
    }

    public const string ApphubDestinationServiceEnvironmentType = "gcp.apphub_destination.service.environment_type";

    public static class ApphubDestinationServiceEnvironmentTypeValues
    {
        public const string Development = "DEVELOPMENT";

        public const string Production = "PRODUCTION";

        public const string Staging = "STAGING";

        public const string Test = "TEST";
    }

    public const string ApphubDestinationServiceId = "gcp.apphub_destination.service.id";

    public const string ApphubDestinationWorkloadCriticalityType = "gcp.apphub_destination.workload.criticality_type";

    public static class ApphubDestinationWorkloadCriticalityTypeValues
    {
        public const string High = "HIGH";

        public const string Low = "LOW";

        public const string Medium = "MEDIUM";

        public const string MissionCritical = "MISSION_CRITICAL";
    }

    public const string ApphubDestinationWorkloadEnvironmentType = "gcp.apphub_destination.workload.environment_type";

    public static class ApphubDestinationWorkloadEnvironmentTypeValues
    {
        public const string Development = "DEVELOPMENT";

        public const string Production = "PRODUCTION";

        public const string Staging = "STAGING";

        public const string Test = "TEST";
    }

    public const string ApphubDestinationWorkloadId = "gcp.apphub_destination.workload.id";

    public const string ClientService = "gcp.client.service";

    public const string CloudRunJobExecution = "gcp.cloud_run.job.execution";

    public const string CloudRunJobTaskIndex = "gcp.cloud_run.job.task_index";

    public const string GceInstanceHostname = "gcp.gce.instance.hostname";

    public const string GceInstanceName = "gcp.gce.instance.name";

    public const string GceInstanceGroupManagerName = "gcp.gce.instance_group_manager.name";

    public const string GceInstanceGroupManagerRegion = "gcp.gce.instance_group_manager.region";

    public const string GceInstanceGroupManagerZone = "gcp.gce.instance_group_manager.zone";
}
