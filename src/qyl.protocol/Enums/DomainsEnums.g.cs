// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-02-27T22:00:11.4248690+00:00
//     Enumeration types for Qyl.Domains
// =============================================================================
// To modify: update TypeSpec in core/specs/ then run: nuke Generate
// =============================================================================

#nullable enable

namespace Qyl.Domains;

/// <summary>Alert firing status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AlertFiringStatus>))]
public enum AlertFiringStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "firing")]
    Firing = 0,
    [System.Runtime.Serialization.EnumMember(Value = "acknowledged")]
    Acknowledged = 1,
    [System.Runtime.Serialization.EnumMember(Value = "resolved")]
    Resolved = 2,
    [System.Runtime.Serialization.EnumMember(Value = "suppressed")]
    Suppressed = 3,
}

/// <summary>Alert rule types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AlertRuleType>))]
public enum AlertRuleType
{
    [System.Runtime.Serialization.EnumMember(Value = "threshold")]
    Threshold = 0,
    [System.Runtime.Serialization.EnumMember(Value = "error_rate")]
    ErrorRate = 1,
    [System.Runtime.Serialization.EnumMember(Value = "new_issue")]
    NewIssue = 2,
    [System.Runtime.Serialization.EnumMember(Value = "regression")]
    Regression = 3,
    [System.Runtime.Serialization.EnumMember(Value = "burn_rate")]
    BurnRate = 4,
    [System.Runtime.Serialization.EnumMember(Value = "anomaly")]
    Anomaly = 5,
    [System.Runtime.Serialization.EnumMember(Value = "custom")]
    Custom = 6,
}

/// <summary>Alert severity levels</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AlertSeverity>))]
public enum AlertSeverity
{
    [System.Runtime.Serialization.EnumMember(Value = "critical")]
    Critical = 0,
    [System.Runtime.Serialization.EnumMember(Value = "warning")]
    Warning = 1,
    [System.Runtime.Serialization.EnumMember(Value = "info")]
    Info = 2,
}

/// <summary>Breadcrumb types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<BreadcrumbType>))]
public enum BreadcrumbType
{
    [System.Runtime.Serialization.EnumMember(Value = "navigation")]
    Navigation = 0,
    [System.Runtime.Serialization.EnumMember(Value = "http")]
    Http = 1,
    [System.Runtime.Serialization.EnumMember(Value = "query")]
    Query = 2,
    [System.Runtime.Serialization.EnumMember(Value = "user")]
    User = 3,
    [System.Runtime.Serialization.EnumMember(Value = "log")]
    Log = 4,
    [System.Runtime.Serialization.EnumMember(Value = "error")]
    Error = 5,
    [System.Runtime.Serialization.EnumMember(Value = "debug")]
    Debug = 6,
    [System.Runtime.Serialization.EnumMember(Value = "default")]
    Default = 7,
}

/// <summary>Fix run status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FixRunStatus>))]
public enum FixRunStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "pending")]
    Pending = 0,
    [System.Runtime.Serialization.EnumMember(Value = "running")]
    Running = 1,
    [System.Runtime.Serialization.EnumMember(Value = "awaiting_approval")]
    AwaitingApproval = 2,
    [System.Runtime.Serialization.EnumMember(Value = "applied")]
    Applied = 3,
    [System.Runtime.Serialization.EnumMember(Value = "rejected")]
    Rejected = 4,
    [System.Runtime.Serialization.EnumMember(Value = "failed")]
    Failed = 5,
}

/// <summary>Fix trigger types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FixTriggerType>))]
public enum FixTriggerType
{
    [System.Runtime.Serialization.EnumMember(Value = "alert")]
    Alert = 0,
    [System.Runtime.Serialization.EnumMember(Value = "manual")]
    Manual = 1,
    [System.Runtime.Serialization.EnumMember(Value = "mcp")]
    Mcp = 2,
    [System.Runtime.Serialization.EnumMember(Value = "scheduled")]
    Scheduled = 3,
}

/// <summary>Generation job types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<GenerationJobType>))]
public enum GenerationJobType
{
    [System.Runtime.Serialization.EnumMember(Value = "full")]
    Full = 0,
    [System.Runtime.Serialization.EnumMember(Value = "incremental")]
    Incremental = 1,
    [System.Runtime.Serialization.EnumMember(Value = "preview")]
    Preview = 2,
}

/// <summary>Handshake session state</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HandshakeState>))]
public enum HandshakeState
{
    [System.Runtime.Serialization.EnumMember(Value = "pending")]
    Pending = 0,
    [System.Runtime.Serialization.EnumMember(Value = "verified")]
    Verified = 1,
    [System.Runtime.Serialization.EnumMember(Value = "expired")]
    Expired = 2,
    [System.Runtime.Serialization.EnumMember(Value = "rejected")]
    Rejected = 3,
}

/// <summary>Issue severity level</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<IssueLevel>))]
public enum IssueLevel
{
    [System.Runtime.Serialization.EnumMember(Value = "debug")]
    Debug = 0,
    [System.Runtime.Serialization.EnumMember(Value = "info")]
    Info = 1,
    [System.Runtime.Serialization.EnumMember(Value = "warning")]
    Warning = 2,
    [System.Runtime.Serialization.EnumMember(Value = "error")]
    Error = 3,
    [System.Runtime.Serialization.EnumMember(Value = "fatal")]
    Fatal = 4,
}

/// <summary>Issue priority</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<IssuePriority>))]
public enum IssuePriority
{
    [System.Runtime.Serialization.EnumMember(Value = "critical")]
    Critical = 0,
    [System.Runtime.Serialization.EnumMember(Value = "high")]
    High = 1,
    [System.Runtime.Serialization.EnumMember(Value = "medium")]
    Medium = 2,
    [System.Runtime.Serialization.EnumMember(Value = "low")]
    Low = 3,
}

/// <summary>Issue lifecycle status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<IssueStatus>))]
public enum IssueStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "unresolved")]
    Unresolved = 0,
    [System.Runtime.Serialization.EnumMember(Value = "acknowledged")]
    Acknowledged = 1,
    [System.Runtime.Serialization.EnumMember(Value = "investigating")]
    Investigating = 2,
    [System.Runtime.Serialization.EnumMember(Value = "in_progress")]
    InProgress = 3,
    [System.Runtime.Serialization.EnumMember(Value = "resolved")]
    Resolved = 4,
    [System.Runtime.Serialization.EnumMember(Value = "ignored")]
    Ignored = 5,
    [System.Runtime.Serialization.EnumMember(Value = "regressed")]
    Regressed = 6,
}

/// <summary>Job lifecycle status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "queued")]
    Queued = 0,
    [System.Runtime.Serialization.EnumMember(Value = "running")]
    Running = 1,
    [System.Runtime.Serialization.EnumMember(Value = "completed")]
    Completed = 2,
    [System.Runtime.Serialization.EnumMember(Value = "failed")]
    Failed = 3,
    [System.Runtime.Serialization.EnumMember(Value = "cancelled")]
    Cancelled = 4,
}

/// <summary>Searchable entity types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SearchEntityType>))]
public enum SearchEntityType
{
    [System.Runtime.Serialization.EnumMember(Value = "span")]
    Span = 0,
    [System.Runtime.Serialization.EnumMember(Value = "log")]
    Log = 1,
    [System.Runtime.Serialization.EnumMember(Value = "issue")]
    Issue = 2,
    [System.Runtime.Serialization.EnumMember(Value = "workflow")]
    Workflow = 3,
    [System.Runtime.Serialization.EnumMember(Value = "deployment")]
    Deployment = 4,
    [System.Runtime.Serialization.EnumMember(Value = "session")]
    Session = 5,
    [System.Runtime.Serialization.EnumMember(Value = "alert")]
    Alert = 6,
    [System.Runtime.Serialization.EnumMember(Value = "fix")]
    Fix = 7,
}

/// <summary>Workflow node types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkflowNodeType>))]
public enum WorkflowNodeType
{
    [System.Runtime.Serialization.EnumMember(Value = "agent")]
    Agent = 0,
    [System.Runtime.Serialization.EnumMember(Value = "tool")]
    Tool = 1,
    [System.Runtime.Serialization.EnumMember(Value = "condition")]
    Condition = 2,
    [System.Runtime.Serialization.EnumMember(Value = "fork")]
    Fork = 3,
    [System.Runtime.Serialization.EnumMember(Value = "join")]
    Join = 4,
    [System.Runtime.Serialization.EnumMember(Value = "approval")]
    Approval = 5,
    [System.Runtime.Serialization.EnumMember(Value = "sub_workflow")]
    SubWorkflow = 6,
    [System.Runtime.Serialization.EnumMember(Value = "transform")]
    Transform = 7,
    [System.Runtime.Serialization.EnumMember(Value = "wait")]
    Wait = 8,
}

/// <summary>Workflow run status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkflowRunStatus>))]
public enum WorkflowRunStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "pending")]
    Pending = 0,
    [System.Runtime.Serialization.EnumMember(Value = "running")]
    Running = 1,
    [System.Runtime.Serialization.EnumMember(Value = "paused")]
    Paused = 2,
    [System.Runtime.Serialization.EnumMember(Value = "completed")]
    Completed = 3,
    [System.Runtime.Serialization.EnumMember(Value = "failed")]
    Failed = 4,
    [System.Runtime.Serialization.EnumMember(Value = "cancelled")]
    Cancelled = 5,
    [System.Runtime.Serialization.EnumMember(Value = "timed_out")]
    TimedOut = 6,
}

/// <summary>Workflow trigger types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkflowTriggerType>))]
public enum WorkflowTriggerType
{
    [System.Runtime.Serialization.EnumMember(Value = "manual")]
    Manual = 0,
    [System.Runtime.Serialization.EnumMember(Value = "alert")]
    Alert = 1,
    [System.Runtime.Serialization.EnumMember(Value = "schedule")]
    Schedule = 2,
    [System.Runtime.Serialization.EnumMember(Value = "event")]
    Event = 3,
    [System.Runtime.Serialization.EnumMember(Value = "api")]
    Api = 4,
    [System.Runtime.Serialization.EnumMember(Value = "mcp")]
    Mcp = 5,
}

/// <summary>Workspace lifecycle status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkspaceStatus>))]
public enum WorkspaceStatus
{
    [System.Runtime.Serialization.EnumMember(Value = "active")]
    Active = 0,
    [System.Runtime.Serialization.EnumMember(Value = "suspended")]
    Suspended = 1,
    [System.Runtime.Serialization.EnumMember(Value = "archived")]
    Archived = 2,
}

