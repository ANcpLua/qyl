// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9037540+00:00
//     Enumeration types for Qyl.Domains.Ops.Cicd
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains.Ops.Cicd;

/// <summary>CI/CD event names</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CicdEventName>))]
public enum CicdEventName
{
    [System.Runtime.Serialization.EnumMember(Value = "cicd.pipeline.start")]
    CicdPipelineStart = 0,
    [System.Runtime.Serialization.EnumMember(Value = "cicd.pipeline.end")]
    CicdPipelineEnd = 1,
    [System.Runtime.Serialization.EnumMember(Value = "cicd.task.start")]
    CicdTaskStart = 2,
    [System.Runtime.Serialization.EnumMember(Value = "cicd.task.end")]
    CicdTaskEnd = 3,
    [System.Runtime.Serialization.EnumMember(Value = "cicd.deployment.start")]
    CicdDeploymentStart = 4,
    [System.Runtime.Serialization.EnumMember(Value = "cicd.deployment.end")]
    CicdDeploymentEnd = 5,
}

/// <summary>CI/CD pipeline status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CicdPipelineStatus>))]
public enum CicdPipelineStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "pending")]
    Pending = 0,
    [System.Runtime.Serialization.EnumMember(Value = "running")]
    Running = 1,
    [System.Runtime.Serialization.EnumMember(Value = "success")]
    Success = 2,
    [System.Runtime.Serialization.EnumMember(Value = "failed")]
    Failed = 3,
    [System.Runtime.Serialization.EnumMember(Value = "cancelled")]
    Cancelled = 4,
    [System.Runtime.Serialization.EnumMember(Value = "skipped")]
    Skipped = 5,
}

/// <summary>CI/CD systems/providers</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CicdSystem>))]
public enum CicdSystem
{
    [System.Runtime.Serialization.EnumMember(Value = "github_actions")]
    GithubActions = 0,
    [System.Runtime.Serialization.EnumMember(Value = "gitlab_ci")]
    GitlabCi = 1,
    [System.Runtime.Serialization.EnumMember(Value = "jenkins")]
    Jenkins = 2,
    [System.Runtime.Serialization.EnumMember(Value = "azure_devops")]
    AzureDevops = 3,
    [System.Runtime.Serialization.EnumMember(Value = "circleci")]
    Circleci = 4,
    [System.Runtime.Serialization.EnumMember(Value = "travis_ci")]
    TravisCi = 5,
    [System.Runtime.Serialization.EnumMember(Value = "bitbucket_pipelines")]
    BitbucketPipelines = 6,
    [System.Runtime.Serialization.EnumMember(Value = "teamcity")]
    Teamcity = 7,
    [System.Runtime.Serialization.EnumMember(Value = "bamboo")]
    Bamboo = 8,
    [System.Runtime.Serialization.EnumMember(Value = "drone_ci")]
    DroneCi = 9,
    [System.Runtime.Serialization.EnumMember(Value = "buildkite")]
    Buildkite = 10,
    [System.Runtime.Serialization.EnumMember(Value = "tekton")]
    Tekton = 11,
    [System.Runtime.Serialization.EnumMember(Value = "argocd")]
    Argocd = 12,
    [System.Runtime.Serialization.EnumMember(Value = "flux")]
    Flux = 13,
    [System.Runtime.Serialization.EnumMember(Value = "spinnaker")]
    Spinnaker = 14,
    [System.Runtime.Serialization.EnumMember(Value = "other")]
    Other = 15,
}

/// <summary>CI/CD trigger types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CicdTriggerType>))]
public enum CicdTriggerType
{
    [System.Runtime.Serialization.EnumMember(Value = "push")]
    Push = 0,
    [System.Runtime.Serialization.EnumMember(Value = "pull_request")]
    PullRequest = 1,
    [System.Runtime.Serialization.EnumMember(Value = "manual")]
    Manual = 2,
    [System.Runtime.Serialization.EnumMember(Value = "schedule")]
    Schedule = 3,
    [System.Runtime.Serialization.EnumMember(Value = "api")]
    Api = 4,
    [System.Runtime.Serialization.EnumMember(Value = "webhook")]
    Webhook = 5,
    [System.Runtime.Serialization.EnumMember(Value = "dependency")]
    Dependency = 6,
    [System.Runtime.Serialization.EnumMember(Value = "tag")]
    Tag = 7,
    [System.Runtime.Serialization.EnumMember(Value = "release")]
    Release = 8,
}

