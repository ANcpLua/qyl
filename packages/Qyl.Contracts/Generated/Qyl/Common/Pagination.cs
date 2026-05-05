#nullable enable

namespace Qyl.Common.Pagination;

public sealed class CursorPaginationParams
{
    public string? Cursor { get; init; }
    public int? Limit { get; init; }
    public Qyl.Common.Pagination.SortOrder? Order { get; init; }
}

public sealed class OffsetPaginationParams
{
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}

public sealed class TimeRangeParams
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public int? Limit { get; init; }
}

public sealed class StreamParams
{
    public DateTimeOffset? Since { get; init; }
    public string? TraceId { get; init; }
    public string? ServiceName { get; init; }
    public int? MaxEventsPerSecond { get; init; }
}

public sealed class AggregationParams
{
    public Qyl.Common.Pagination.TimeBucket? Bucket { get; init; }
    public IReadOnlyList<string>? GroupBy { get; init; }
    public string? Filter { get; init; }
}

public sealed class CursorPageTrace
{
    public required IReadOnlyList<Qyl.OTel.Traces.Trace> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageSpanRecord
{
    public required IReadOnlyList<Qyl.Storage.SpanRecord> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageLogRecord
{
    public required IReadOnlyList<Qyl.OTel.Logs.LogRecord> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageMetricMetadata
{
    public required IReadOnlyList<Qyl.Api.MetricMetadata> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageSessionEntity
{
    public required IReadOnlyList<Qyl.Domains.Observe.Session.SessionEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageErrorEntity
{
    public required IReadOnlyList<Qyl.Domains.Observe.Error.ErrorEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageDeploymentEntity
{
    public required IReadOnlyList<Qyl.Domains.Ops.Deployment.DeploymentEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageServiceInfo
{
    public required IReadOnlyList<Qyl.Api.ServiceInfo> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageOperationInfo
{
    public required IReadOnlyList<Qyl.Api.OperationInfo> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageProjectEntity
{
    public required IReadOnlyList<Qyl.Domains.Workspace.ProjectEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageGenerationProfileEntity
{
    public required IReadOnlyList<Qyl.Domains.Configurator.GenerationProfileEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageErrorIssueEntity
{
    public required IReadOnlyList<Qyl.Domains.Issues.ErrorIssueEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageErrorIssueEventEntity
{
    public required IReadOnlyList<Qyl.Domains.Issues.ErrorIssueEventEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageWorkflowRunEntity
{
    public required IReadOnlyList<Qyl.Domains.Workflow.WorkflowRunEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageWorkflowNodeEntity
{
    public required IReadOnlyList<Qyl.Domains.Workflow.WorkflowNodeEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageAlertRuleEntity
{
    public required IReadOnlyList<Qyl.Domains.Alerting.AlertRuleEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageAlertFiringEntity
{
    public required IReadOnlyList<Qyl.Domains.Alerting.AlertFiringEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public sealed class CursorPageFixRunEntity
{
    public required IReadOnlyList<Qyl.Domains.Alerting.FixRunEntity> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PrevCursor { get; init; }
    public required bool HasMore { get; init; }
}

public enum SortOrder
{
    Asc,
    Desc
}

public enum TimeBucket
{
    Minute,
    FiveMinutes,
    FifteenMinutes,
    Hour,
    Day,
    Week,
    Auto
}
