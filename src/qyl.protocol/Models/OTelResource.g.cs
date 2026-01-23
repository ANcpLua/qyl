// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-01-23T04:40:32.9058740+00:00
//     Models for Qyl.OTel.Resource
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Qyl.OTel.Resource;

/// <summary>Resource describes the entity producing telemetry</summary>
public sealed record Resource
{
    /// <summary>Service name (required)</summary>
    [JsonPropertyName("service.name")]
    public required string ServiceName { get; init; }

    /// <summary>Service namespace for grouping</summary>
    [JsonPropertyName("service.namespace")]
    public string? ServiceNamespace { get; init; }

    /// <summary>Service instance ID (unique per instance)</summary>
    [JsonPropertyName("service.instance.id")]
    public string? ServiceInstanceId { get; init; }

    /// <summary>Service version</summary>
    [JsonPropertyName("service.version")]
    public global::Qyl.Common.SemVer? ServiceVersion { get; init; }

    /// <summary>Telemetry SDK name</summary>
    [JsonPropertyName("telemetry.sdk.name")]
    public string? TelemetrySdkName { get; init; }

    /// <summary>Telemetry SDK language</summary>
    [JsonPropertyName("telemetry.sdk.language")]
    public global::Qyl.OTel.Enums.TelemetrySdkLanguage? TelemetrySdkLanguage { get; init; }

    /// <summary>Telemetry SDK version</summary>
    [JsonPropertyName("telemetry.sdk.version")]
    public global::Qyl.Common.SemVer? TelemetrySdkVersion { get; init; }

    /// <summary>Auto-instrumentation agent name</summary>
    [JsonPropertyName("telemetry.auto.version")]
    public global::Qyl.Common.SemVer? TelemetryAutoVersion { get; init; }

    /// <summary>Deployment environment (e.g., production, staging)</summary>
    [JsonPropertyName("deployment.environment.name")]
    public string? DeploymentEnvironmentName { get; init; }

    /// <summary>Cloud provider</summary>
    [JsonPropertyName("cloud.provider")]
    public global::Qyl.OTel.Resource.CloudProvider? CloudProvider { get; init; }

    /// <summary>Cloud region</summary>
    [JsonPropertyName("cloud.region")]
    public string? CloudRegion { get; init; }

    /// <summary>Cloud availability zone</summary>
    [JsonPropertyName("cloud.availability_zone")]
    public string? CloudAvailabilityZone { get; init; }

    /// <summary>Cloud account ID</summary>
    [JsonPropertyName("cloud.account.id")]
    public string? CloudAccountId { get; init; }

    /// <summary>Cloud platform (e.g., aws_ecs, gcp_cloud_run)</summary>
    [JsonPropertyName("cloud.platform")]
    public string? CloudPlatform { get; init; }

    /// <summary>Host name</summary>
    [JsonPropertyName("host.name")]
    public string? HostName { get; init; }

    /// <summary>Host ID</summary>
    [JsonPropertyName("host.id")]
    public string? HostId { get; init; }

    /// <summary>Host type (e.g., n1-standard-1)</summary>
    [JsonPropertyName("host.type")]
    public string? HostType { get; init; }

    /// <summary>Host architecture (e.g., amd64, arm64)</summary>
    [JsonPropertyName("host.arch")]
    public global::Qyl.OTel.Resource.HostArch? HostArch { get; init; }

    /// <summary>Operating system type</summary>
    [JsonPropertyName("os.type")]
    public global::Qyl.OTel.Resource.OsType? OsType { get; init; }

    /// <summary>Operating system description</summary>
    [JsonPropertyName("os.description")]
    public string? OsDescription { get; init; }

    /// <summary>Operating system version</summary>
    [JsonPropertyName("os.version")]
    public string? OsVersion { get; init; }

    /// <summary>Process ID</summary>
    [JsonPropertyName("process.pid")]
    public long? ProcessPid { get; init; }

    /// <summary>Process executable name</summary>
    [JsonPropertyName("process.executable.name")]
    public string? ProcessExecutableName { get; init; }

    /// <summary>Process command line</summary>
    [JsonPropertyName("process.command_line")]
    public string? ProcessCommandLine { get; init; }

    /// <summary>Process runtime name</summary>
    [JsonPropertyName("process.runtime.name")]
    public string? ProcessRuntimeName { get; init; }

    /// <summary>Process runtime version</summary>
    [JsonPropertyName("process.runtime.version")]
    public string? ProcessRuntimeVersion { get; init; }

    /// <summary>Container ID</summary>
    [JsonPropertyName("container.id")]
    public string? ContainerId { get; init; }

    /// <summary>Container name</summary>
    [JsonPropertyName("container.name")]
    public string? ContainerName { get; init; }

    /// <summary>Container image name</summary>
    [JsonPropertyName("container.image.name")]
    public string? ContainerImageName { get; init; }

    /// <summary>Container image tag</summary>
    [JsonPropertyName("container.image.tag")]
    public string? ContainerImageTag { get; init; }

    /// <summary>Kubernetes cluster name</summary>
    [JsonPropertyName("k8s.cluster.name")]
    public string? K8sClusterName { get; init; }

    /// <summary>Kubernetes namespace</summary>
    [JsonPropertyName("k8s.namespace.name")]
    public string? K8sNamespaceName { get; init; }

    /// <summary>Kubernetes pod name</summary>
    [JsonPropertyName("k8s.pod.name")]
    public string? K8sPodName { get; init; }

    /// <summary>Kubernetes pod UID</summary>
    [JsonPropertyName("k8s.pod.uid")]
    public string? K8sPodUid { get; init; }

    /// <summary>Kubernetes deployment name</summary>
    [JsonPropertyName("k8s.deployment.name")]
    public string? K8sDeploymentName { get; init; }

    /// <summary>Additional resource attributes</summary>
    [JsonPropertyName("attributes")]
    public IReadOnlyList<global::Qyl.Common.Attribute>? Attributes { get; init; }

    /// <summary>Dropped attributes count</summary>
    [JsonPropertyName("dropped_attributes_count")]
    public global::Qyl.Common.Count? DroppedAttributesCount { get; init; }

}

