// =============================================================================
// QYL v2.0 - CI/CD Semantic Conventions (OTel v1.38)
// =============================================================================
// Continuous Integration/Deployment telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Ops.Cicd;

// =============================================================================
// CI/CD Attributes
// =============================================================================

@doc("CI/CD pipeline attributes")
model CicdAttributes {
  @doc("CI/CD pipeline name")
  @encodedName("application/json", "cicd.pipeline.name")
  pipelineName?: string;

  @doc("Pipeline run ID")
  @encodedName("application/json", "cicd.pipeline.run.id")
  pipelineRunId?: string;

  @doc("Pipeline task/job name")
  @encodedName("application/json", "cicd.pipeline.task.name")
  pipelineTaskName?: string;

  @doc("Pipeline task run ID")
  @encodedName("application/json", "cicd.pipeline.task.run.id")
  pipelineTaskRunId?: string;

  @doc("Pipeline task run URL")
  @encodedName("application/json", "cicd.pipeline.task.run.url.full")
  pipelineTaskRunUrl?: UrlString;

  @doc("Pipeline task type")
  @encodedName("application/json", "cicd.pipeline.task.type")
  pipelineTaskType?: CicdTaskType;
}

// =============================================================================
// CI/CD Enumerations
// =============================================================================

@doc("CI/CD task types")
enum CicdTaskType {
  @doc("Build task")
  build: "build",

  @doc("Test task")
  test: "test",

  @doc("Deploy task")
  deploy: "deploy",
}

@doc("CI/CD pipeline status")
enum CicdPipelineStatus {
  @doc("Pipeline is pending")
  pending: "pending",

  @doc("Pipeline is running")
  running: "running",

  @doc("Pipeline succeeded")
  success: "success",

  @doc("Pipeline failed")
  failed: "failed",

  @doc("Pipeline was cancelled")
  cancelled: "cancelled",

  @doc("Pipeline was skipped")
  skipped: "skipped",
}

@doc("CI/CD systems/providers")
enum CicdSystem {
  @doc("GitHub Actions")
  githubActions: "github_actions",

  @doc("GitLab CI/CD")
  gitlabCi: "gitlab_ci",

  @doc("Jenkins")
  jenkins: "jenkins",

  @doc("Azure DevOps Pipelines")
  azureDevops: "azure_devops",

  @doc("CircleCI")
  circleci: "circleci",

  @doc("Travis CI")
  travisCi: "travis_ci",

  @doc("Bitbucket Pipelines")
  bitbucketPipelines: "bitbucket_pipelines",

  @doc("TeamCity")
  teamcity: "teamcity",

  @doc("Bamboo")
  bamboo: "bamboo",

  @doc("Drone CI")
  droneCi: "drone_ci",

  @doc("Buildkite")
  buildkite: "buildkite",

  @doc("Tekton")
  tekton: "tekton",

  @doc("ArgoCD")
  argocd: "argocd",

  @doc("Flux")
  flux: "flux",

  @doc("Spinnaker")
  spinnaker: "spinnaker",

  @doc("Other")
  other: "other",
}

// =============================================================================
// CI/CD Events
// =============================================================================

@doc("Pipeline run event")
model PipelineRunEvent {
  @doc("Event name")
  @encodedName("application/json", "event.name")
  eventName: CicdEventName;

  @doc("Pipeline name")
  @encodedName("application/json", "cicd.pipeline.name")
  pipelineName: string;

  @doc("Pipeline run ID")
  @encodedName("application/json", "cicd.pipeline.run.id")
  pipelineRunId: string;

  @doc("Pipeline status")
  status: CicdPipelineStatus;

  @doc("CI/CD system")
  system: CicdSystem;

  @doc("Trigger type")
  @encodedName("application/json", "trigger_type")
  triggerType?: CicdTriggerType;

  @doc("Git branch")
  @encodedName("application/json", "vcs.repository.ref.name")
  branch?: string;

  @doc("Git commit SHA")
  @encodedName("application/json", "vcs.repository.ref.revision")
  commitSha?: string;

  @doc("Duration in seconds")
  @encodedName("application/json", "duration_s")
  durationS?: DurationS;

  @doc("Event timestamp")
  timestamp: utcDateTime;
}

@doc("CI/CD event names")
enum CicdEventName {
  @doc("Pipeline started")
  pipelineStart: "cicd.pipeline.start",

  @doc("Pipeline completed")
  pipelineEnd: "cicd.pipeline.end",

  @doc("Task started")
  taskStart: "cicd.task.start",

  @doc("Task completed")
  taskEnd: "cicd.task.end",

  @doc("Deployment started")
  deploymentStart: "cicd.deployment.start",

  @doc("Deployment completed")
  deploymentEnd: "cicd.deployment.end",
}

@doc("CI/CD trigger types")
enum CicdTriggerType {
  @doc("Push to branch")
  push: "push",

  @doc("Pull request")
  pullRequest: "pull_request",

  @doc("Manual trigger")
  manual: "manual",

  @doc("Scheduled trigger")
  schedule: "schedule",

  @doc("API trigger")
  api: "api",

  @doc("Webhook trigger")
  webhook: "webhook",

  @doc("Dependency update")
  dependency: "dependency",

  @doc("Tag creation")
  tag: "tag",

  @doc("Release creation")
  release: "release",
}

// =============================================================================
// CI/CD Metrics
// =============================================================================

@doc("Pipeline duration metric")
model PipelineDurationMetric {
  @doc("Metric name")
  name: "cicd.pipeline.duration";

  @doc("Metric unit (seconds)")
  unit: "s";

  @doc("Pipeline name")
  @encodedName("application/json", "cicd.pipeline.name")
  pipelineName: string;

  @doc("Pipeline status")
  @encodedName("application/json", "cicd.pipeline.status")
  pipelineStatus: CicdPipelineStatus;

  @doc("CI/CD system")
  @encodedName("application/json", "cicd.system")
  system: CicdSystem;
}

@doc("Pipeline runs metric")
model PipelineRunsMetric {
  @doc("Metric name")
  name: "cicd.pipeline.runs";

  @doc("Metric unit")
  unit: "{run}";

  @doc("Pipeline name")
  @encodedName("application/json", "cicd.pipeline.name")
  pipelineName: string;

  @doc("Pipeline status")
  @encodedName("application/json", "cicd.pipeline.status")
  pipelineStatus: CicdPipelineStatus;
}

@doc("Deployment frequency metric")
model DeploymentFrequencyMetric {
  @doc("Metric name")
  name: "cicd.deployment.frequency";

  @doc("Metric unit")
  unit: "{deployment}";

  @doc("Environment")
  @encodedName("application/json", "deployment.environment.name")
  environmentName: string;

  @doc("Service name")
  @encodedName("application/json", "service.name")
  serviceName?: string;
}
