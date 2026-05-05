

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Cloud;

public static class CloudAttributes
{
    public const string AccountId = "cloud.account.id";

    public const string AvailabilityZone = "cloud.availability_zone";

    public const string Platform = "cloud.platform";

    public static class PlatformValues
    {
        public const string AkamaiCloudCompute = "akamai_cloud.compute";

        public const string AlibabaCloudEcs = "alibaba_cloud_ecs";

        public const string AlibabaCloudFc = "alibaba_cloud_fc";

        public const string AlibabaCloudOpenshift = "alibaba_cloud_openshift";

        public const string AwsAppRunner = "aws_app_runner";

        public const string AwsEc2 = "aws_ec2";

        public const string AwsEcs = "aws_ecs";

        public const string AwsEks = "aws_eks";

        public const string AwsElasticBeanstalk = "aws_elastic_beanstalk";

        public const string AwsLambda = "aws_lambda";

        public const string AwsOpenshift = "aws_openshift";

        public const string AzureAks = "azure.aks";

        public const string AzureAppService = "azure.app_service";

        public const string AzureContainerApps = "azure.container_apps";

        public const string AzureContainerInstances = "azure.container_instances";

        public const string AzureFunctions = "azure.functions";

        public const string AzureOpenshift = "azure.openshift";

        public const string AzureVm = "azure.vm";

        public const string GcpAgentEngine = "gcp.agent_engine";

        public const string GcpAppEngine = "gcp_app_engine";

        public const string GcpBareMetalSolution = "gcp_bare_metal_solution";

        public const string GcpCloudFunctions = "gcp_cloud_functions";

        public const string GcpCloudRun = "gcp_cloud_run";

        public const string GcpComputeEngine = "gcp_compute_engine";

        public const string GcpKubernetesEngine = "gcp_kubernetes_engine";

        public const string GcpOpenshift = "gcp_openshift";

        public const string HetznerCloudServer = "hetzner.cloud_server";

        public const string IbmCloudOpenshift = "ibm_cloud_openshift";

        public const string OracleCloudCompute = "oracle_cloud_compute";

        public const string OracleCloudOke = "oracle_cloud_oke";

        public const string TencentCloudCvm = "tencent_cloud_cvm";

        public const string TencentCloudEks = "tencent_cloud_eks";

        public const string TencentCloudScf = "tencent_cloud_scf";

        public const string VultrCloudCompute = "vultr.cloud_compute";
    }

    public const string Provider = "cloud.provider";

    public static class ProviderValues
    {
        public const string AkamaiCloud = "akamai_cloud";

        public const string AlibabaCloud = "alibaba_cloud";

        public const string Aws = "aws";

        public const string Azure = "azure";

        public const string Gcp = "gcp";

        public const string Heroku = "heroku";

        public const string Hetzner = "hetzner";

        public const string IbmCloud = "ibm_cloud";

        public const string OracleCloud = "oracle_cloud";

        public const string TencentCloud = "tencent_cloud";

        public const string Vultr = "vultr";
    }

    public const string Region = "cloud.region";

    public const string ResourceId = "cloud.resource_id";
}
