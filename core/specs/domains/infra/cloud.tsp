// =============================================================================
// QYL v2.0 - Cloud Semantic Conventions (OTel v1.38)
// =============================================================================
// Cloud provider resource attributes.
// =============================================================================

import "../../common/types.tsp";
import "../../otel/resource.tsp";

using Qyl.Common;
using Qyl.OTel.Resource;

namespace Qyl.Domains.Infra.Cloud;

// =============================================================================
// Cloud Resource Attributes
// =============================================================================

@doc("Cloud resource attributes")
model CloudAttributes {
  @doc("Cloud provider name")
  @encodedName("application/json", "cloud.provider")
  provider?: CloudProvider;

  @doc("Cloud account ID")
  @encodedName("application/json", "cloud.account.id")
  accountId?: string;

  @doc("Cloud region")
  @encodedName("application/json", "cloud.region")
  region?: string;

  @doc("Cloud availability zone")
  @encodedName("application/json", "cloud.availability_zone")
  availabilityZone?: string;

  @doc("Cloud platform (specific service/product)")
  @encodedName("application/json", "cloud.platform")
  platform?: CloudPlatform;

  @doc("Cloud resource ID")
  @encodedName("application/json", "cloud.resource_id")
  resourceId?: string;
}

// =============================================================================
// Cloud Platform Enumerations
// =============================================================================

@doc("Cloud platforms (specific services)")
enum CloudPlatform {
  // AWS Platforms
  @doc("AWS EC2")
  awsEc2: "aws_ec2",

  @doc("AWS ECS")
  awsEcs: "aws_ecs",

  @doc("AWS EKS")
  awsEks: "aws_eks",

  @doc("AWS Lambda")
  awsLambda: "aws_lambda",

  @doc("AWS Elastic Beanstalk")
  awsElasticBeanstalk: "aws_elastic_beanstalk",

  @doc("AWS App Runner")
  awsAppRunner: "aws_app_runner",

  // Azure Platforms
  @doc("Azure VM")
  azureVm: "azure_vm",

  @doc("Azure Container Instances")
  azureContainerInstances: "azure_container_instances",

  @doc("Azure AKS")
  azureAks: "azure_aks",

  @doc("Azure Functions")
  azureFunctions: "azure_functions",

  @doc("Azure App Service")
  azureAppService: "azure_app_service",

  @doc("Azure Container Apps")
  azureContainerApps: "azure_container_apps",

  // GCP Platforms
  @doc("GCP Compute Engine")
  gcpComputeEngine: "gcp_compute_engine",

  @doc("GCP Cloud Run")
  gcpCloudRun: "gcp_cloud_run",

  @doc("GCP GKE")
  gcpGke: "gcp_kubernetes_engine",

  @doc("GCP Cloud Functions")
  gcpCloudFunctions: "gcp_cloud_functions",

  @doc("GCP App Engine")
  gcpAppEngine: "gcp_app_engine",

  // Alibaba Cloud
  @doc("Alibaba Cloud ECS")
  alibabaCloudEcs: "alibaba_cloud_ecs",

  @doc("Alibaba Cloud Function Compute")
  alibabaCloudFc: "alibaba_cloud_fc",

  // Tencent Cloud
  @doc("Tencent Cloud CVM")
  tencentCloudCvm: "tencent_cloud_cvm",

  @doc("Tencent Cloud SCF")
  tencentCloudScf: "tencent_cloud_scf",

  // IBM Cloud
  @doc("IBM Cloud VM")
  ibmCloudVirtualServer: "ibm_cloud_virtual_server",
}

// =============================================================================
// Cloud-Specific Models
// =============================================================================

@doc("AWS resource attributes")
model AwsAttributes {
  @doc("AWS account ID")
  @encodedName("application/json", "cloud.account.id")
  accountId?: string;

  @doc("AWS region")
  @encodedName("application/json", "cloud.region")
  region?: string;

  @doc("AWS availability zone")
  @encodedName("application/json", "cloud.availability_zone")
  availabilityZone?: string;

  @doc("EC2 instance ID")
  @encodedName("application/json", "aws.ec2.instance_id")
  ec2InstanceId?: string;

  @doc("EC2 instance type")
  @encodedName("application/json", "aws.ec2.instance_type")
  ec2InstanceType?: string;

  @doc("EC2 AMI ID")
  @encodedName("application/json", "aws.ec2.ami_id")
  ec2AmiId?: string;

  @doc("ECS cluster ARN")
  @encodedName("application/json", "aws.ecs.cluster.arn")
  ecsClusterArn?: string;

  @doc("ECS container ARN")
  @encodedName("application/json", "aws.ecs.container.arn")
  ecsContainerArn?: string;

  @doc("ECS task ARN")
  @encodedName("application/json", "aws.ecs.task.arn")
  ecsTaskArn?: string;

  @doc("ECS task family")
  @encodedName("application/json", "aws.ecs.task.family")
  ecsTaskFamily?: string;

  @doc("ECS launch type")
  @encodedName("application/json", "aws.ecs.launchtype")
  ecsLaunchType?: AwsEcsLaunchType;

  @doc("EKS cluster ARN")
  @encodedName("application/json", "aws.eks.cluster.arn")
  eksClusterArn?: string;

  @doc("Lambda function ARN")
  @encodedName("application/json", "aws.lambda.function.arn")
  lambdaFunctionArn?: string;

  @doc("Lambda invoked ARN")
  @encodedName("application/json", "aws.lambda.invoked_arn")
  lambdaInvokedArn?: string;
}

@doc("AWS ECS launch types")
enum AwsEcsLaunchType {
  @doc("EC2 launch type")
  ec2: "EC2",

  @doc("Fargate launch type")
  fargate: "Fargate",
}

@doc("Azure resource attributes")
model AzureAttributes {
  @doc("Azure subscription ID")
  @encodedName("application/json", "azure.subscription.id")
  subscriptionId?: string;

  @doc("Azure resource group")
  @encodedName("application/json", "azure.resource_group.name")
  resourceGroupName?: string;

  @doc("Azure VM ID")
  @encodedName("application/json", "azure.vm.id")
  vmId?: string;

  @doc("Azure VM name")
  @encodedName("application/json", "azure.vm.name")
  vmName?: string;

  @doc("Azure VM size")
  @encodedName("application/json", "azure.vm.size")
  vmSize?: string;

  @doc("Azure VM scale set name")
  @encodedName("application/json", "azure.vm.scale_set.name")
  vmScaleSetName?: string;

  @doc("Azure AKS cluster name")
  @encodedName("application/json", "azure.aks.cluster.name")
  aksClusterName?: string;

  @doc("Azure App Service name")
  @encodedName("application/json", "azure.app_service.name")
  appServiceName?: string;

  @doc("Azure Function name")
  @encodedName("application/json", "azure.function.name")
  functionName?: string;
}

@doc("GCP resource attributes")
model GcpAttributes {
  @doc("GCP project ID")
  @encodedName("application/json", "gcp.project.id")
  projectId?: string;

  @doc("GCP zone")
  @encodedName("application/json", "gcp.zone")
  zone?: string;

  @doc("GCE instance ID")
  @encodedName("application/json", "gcp.gce.instance.id")
  gceInstanceId?: string;

  @doc("GCE instance name")
  @encodedName("application/json", "gcp.gce.instance.name")
  gceInstanceName?: string;

  @doc("GCE machine type")
  @encodedName("application/json", "gcp.gce.machine_type")
  gceMachineType?: string;

  @doc("GKE cluster name")
  @encodedName("application/json", "gcp.gke.cluster.name")
  gkeClusterName?: string;

  @doc("Cloud Run service name")
  @encodedName("application/json", "gcp.cloud_run.service.name")
  cloudRunServiceName?: string;

  @doc("Cloud Run revision")
  @encodedName("application/json", "gcp.cloud_run.revision")
  cloudRunRevision?: string;

  @doc("Cloud Function name")
  @encodedName("application/json", "gcp.cloud_function.name")
  cloudFunctionName?: string;
}
