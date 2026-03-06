// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2392370+00:00
//     Enumeration types for Qyl.Domains.Ops.Deployment
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains.Ops.Deployment;

/// <summary>Deployment environments</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentEnvironment>))]
public enum DeploymentEnvironment
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("development")]
    Development = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("testing")]
    Testing = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("staging")]
    Staging = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("production")]
    Production = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("preview")]
    Preview = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("canary")]
    Canary = 5,
}

/// <summary>Deployment event names</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentEventName>))]
public enum DeploymentEventName
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment.started")]
    DeploymentStarted = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment.completed")]
    DeploymentCompleted = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment.failed")]
    DeploymentFailed = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment.rolled_back")]
    DeploymentRolledBack = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment.health_check.passed")]
    DeploymentHealthCheckPassed = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment.health_check.failed")]
    DeploymentHealthCheckFailed = 5,
}

/// <summary>Deployment status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentStatus>))]
public enum DeploymentStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("pending")]
    Pending = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("in_progress")]
    InProgress = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("success")]
    Success = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("failed")]
    Failed = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rolled_back")]
    RolledBack = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("cancelled")]
    Cancelled = 5,
}

/// <summary>Deployment strategies</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentStrategy>))]
public enum DeploymentStrategy
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rolling")]
    Rolling = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("blue_green")]
    BlueGreen = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("canary")]
    Canary = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("recreate")]
    Recreate = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ab_test")]
    AbTest = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("shadow")]
    Shadow = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("feature_flag")]
    FeatureFlag = 6,
}

