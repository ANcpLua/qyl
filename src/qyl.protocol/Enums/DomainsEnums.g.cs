// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    core/openapi/openapi.yaml
//     Generated: 2026-03-06T15:59:59.2388370+00:00
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
    [System.Text.Json.Serialization.JsonStringEnumMemberName("firing")]
    Firing = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("acknowledged")]
    Acknowledged = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("resolved")]
    Resolved = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("suppressed")]
    Suppressed = 3,
}

/// <summary>Alert rule types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AlertRuleType>))]
public enum AlertRuleType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("threshold")]
    Threshold = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("error_rate")]
    ErrorRate = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("new_issue")]
    NewIssue = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("regression")]
    Regression = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("burn_rate")]
    BurnRate = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("anomaly")]
    Anomaly = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("custom")]
    Custom = 6,
}

/// <summary>Alert severity levels</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AlertSeverity>))]
public enum AlertSeverity
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("critical")]
    Critical = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("warning")]
    Warning = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("info")]
    Info = 2,
}

/// <summary>Breadcrumb types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<BreadcrumbType>))]
public enum BreadcrumbType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("navigation")]
    Navigation = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("http")]
    Http = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("query")]
    Query = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("user")]
    User = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("log")]
    Log = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("error")]
    Error = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("debug")]
    Debug = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("default")]
    Default = 7,
}

/// <summary>Fix run status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FixRunStatus>))]
public enum FixRunStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("pending")]
    Pending = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("running")]
    Running = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("awaiting_approval")]
    AwaitingApproval = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("applied")]
    Applied = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rejected")]
    Rejected = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("failed")]
    Failed = 5,
}

/// <summary>Fix trigger types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FixTriggerType>))]
public enum FixTriggerType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("alert")]
    Alert = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("manual")]
    Manual = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("mcp")]
    Mcp = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("scheduled")]
    Scheduled = 3,
}

/// <summary>Generation job types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<GenerationJobType>))]
public enum GenerationJobType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("full")]
    Full = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("incremental")]
    Incremental = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("preview")]
    Preview = 2,
}

/// <summary>Handshake session state</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HandshakeState>))]
public enum HandshakeState
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("pending")]
    Pending = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("verified")]
    Verified = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("expired")]
    Expired = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("rejected")]
    Rejected = 3,
}

/// <summary>Issue severity level</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<IssueLevel>))]
public enum IssueLevel
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("debug")]
    Debug = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("info")]
    Info = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("warning")]
    Warning = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("error")]
    Error = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("fatal")]
    Fatal = 4,
}

/// <summary>Issue priority</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<IssuePriority>))]
public enum IssuePriority
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("critical")]
    Critical = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("high")]
    High = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("medium")]
    Medium = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("low")]
    Low = 3,
}

/// <summary>Issue lifecycle status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<IssueStatus>))]
public enum IssueStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("unresolved")]
    Unresolved = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("acknowledged")]
    Acknowledged = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("investigating")]
    Investigating = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("in_progress")]
    InProgress = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("resolved")]
    Resolved = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("ignored")]
    Ignored = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("regressed")]
    Regressed = 6,
}

/// <summary>Job lifecycle status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<JobStatus>))]
public enum JobStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("queued")]
    Queued = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("running")]
    Running = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("completed")]
    Completed = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("failed")]
    Failed = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("cancelled")]
    Cancelled = 4,
}

/// <summary>Searchable entity types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SearchEntityType>))]
public enum SearchEntityType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("span")]
    Span = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("log")]
    Log = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("issue")]
    Issue = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("workflow")]
    Workflow = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("deployment")]
    Deployment = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("session")]
    Session = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("alert")]
    Alert = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("fix")]
    Fix = 7,
}

/// <summary>Workflow node types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkflowNodeType>))]
public enum WorkflowNodeType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("agent")]
    Agent = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("tool")]
    Tool = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("condition")]
    Condition = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("fork")]
    Fork = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("join")]
    Join = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("approval")]
    Approval = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("sub_workflow")]
    SubWorkflow = 6,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("transform")]
    Transform = 7,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("wait")]
    Wait = 8,
}

/// <summary>Workflow run status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkflowRunStatus>))]
public enum WorkflowRunStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("pending")]
    Pending = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("running")]
    Running = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("paused")]
    Paused = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("completed")]
    Completed = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("failed")]
    Failed = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("cancelled")]
    Cancelled = 5,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("timed_out")]
    TimedOut = 6,
}

/// <summary>Workflow trigger types</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkflowTriggerType>))]
public enum WorkflowTriggerType
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("manual")]
    Manual = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("alert")]
    Alert = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("schedule")]
    Schedule = 2,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("event")]
    Event = 3,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("api")]
    Api = 4,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("mcp")]
    Mcp = 5,
}

/// <summary>Workspace lifecycle status</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WorkspaceStatus>))]
public enum WorkspaceStatus
{
    [System.Text.Json.Serialization.JsonStringEnumMemberName("active")]
    Active = 0,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("suspended")]
    Suspended = 1,
    [System.Text.Json.Serialization.JsonStringEnumMemberName("archived")]
    Archived = 2,
}

