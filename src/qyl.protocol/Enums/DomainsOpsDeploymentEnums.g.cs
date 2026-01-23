// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9037900+00:00
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
    [System.Runtime.Serialization.EnumMember(Value = "development")]
    Development = 0,
    [System.Runtime.Serialization.EnumMember(Value = "testing")]
    Testing = 1,
    [System.Runtime.Serialization.EnumMember(Value = "staging")]
    Staging = 2,
    [System.Runtime.Serialization.EnumMember(Value = "production")]
    Production = 3,
    [System.Runtime.Serialization.EnumMember(Value = "preview")]
    Preview = 4,
    [System.Runtime.Serialization.EnumMember(Value = "canary")]
    Canary = 5,
}

/// <summary>Deployment event names</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentEventName>))]
public enum DeploymentEventName
{
    [System.Runtime.Serialization.EnumMember(Value = "deployment.started")]
    DeploymentStarted = 0,
    [System.Runtime.Serialization.EnumMember(Value = "deployment.completed")]
    DeploymentCompleted = 1,
    [System.Runtime.Serialization.EnumMember(Value = "deployment.failed")]
    DeploymentFailed = 2,
    [System.Runtime.Serialization.EnumMember(Value = "deployment.rolled_back")]
    DeploymentRolledBack = 3,
    [System.Runtime.Serialization.EnumMember(Value = "deployment.health_check.passed")]
    DeploymentHealthCheckPassed = 4,
    [System.Runtime.Serialization.EnumMember(Value = "deployment.health_check.failed")]
    DeploymentHealthCheckFailed = 5,
}

/// <summary>Deployment status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentStatus>))]
public enum DeploymentStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "pending")]
    Pending = 0,
    [System.Runtime.Serialization.EnumMember(Value = "in_progress")]
    InProgress = 1,
    [System.Runtime.Serialization.EnumMember(Value = "success")]
    Success = 2,
    [System.Runtime.Serialization.EnumMember(Value = "failed")]
    Failed = 3,
    [System.Runtime.Serialization.EnumMember(Value = "rolled_back")]
    RolledBack = 4,
    [System.Runtime.Serialization.EnumMember(Value = "cancelled")]
    Cancelled = 5,
}

/// <summary>Deployment strategies</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeploymentStrategy>))]
public enum DeploymentStrategy
{
    [System.Runtime.Serialization.EnumMember(Value = "rolling")]
    Rolling = 0,
    [System.Runtime.Serialization.EnumMember(Value = "blue_green")]
    BlueGreen = 1,
    [System.Runtime.Serialization.EnumMember(Value = "canary")]
    Canary = 2,
    [System.Runtime.Serialization.EnumMember(Value = "recreate")]
    Recreate = 3,
    [System.Runtime.Serialization.EnumMember(Value = "ab_test")]
    AbTest = 4,
    [System.Runtime.Serialization.EnumMember(Value = "shadow")]
    Shadow = 5,
    [System.Runtime.Serialization.EnumMember(Value = "feature_flag")]
    FeatureFlag = 6,
}

