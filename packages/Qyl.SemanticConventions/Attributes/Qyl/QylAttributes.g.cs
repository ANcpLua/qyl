

namespace Qyl.SemanticConventions.Attributes.Qyl;

public static class QylAttributes
{
    public const string ApiKeyId = "qyl.api_key.id";

    public const string AuthInstanceId = "qyl.auth.instance_id";

    public const string AuthKeycloakClaims = "qyl.auth.keycloak_claims";

    public const string CapabilityId = "qyl.capability.id";

    public const string CapabilityKind = "qyl.capability.kind";

    public static class CapabilityKindValues
    {
        public const string FollowUp = "FollowUp";
        public const string Starting = "Starting";
    }

    public const string CheckInDurationMs = "qyl.check_in.duration_ms";

    public const string CheckInMonitorSlug = "qyl.check_in.monitor_slug";

    public const string CheckInScheduleCron = "qyl.check_in.schedule_cron";

    public const string CheckInScheduleIntervalMinutes = "qyl.check_in.schedule_interval_minutes";

    public const string CheckInStatus = "qyl.check_in.status";

    public static class CheckInStatusValues
    {
        public const string Error = "error";
        public const string InProgress = "in_progress";
        public const string Missed = "missed";
        public const string Ok = "ok";
        public const string Timeout = "timeout";
    }

    public const string DuckdbDroppedJobsTotal = "qyl.duckdb.dropped_jobs_total";

    public const string DuckdbDroppedSpansTotal = "qyl.duckdb.dropped_spans_total";

    public const string FeedbackContactEmail = "qyl.feedback.contact_email";

    public const string FeedbackEventId = "qyl.feedback.event_id";

    public const string FeedbackId = "qyl.feedback.id";

    public const string FeedbackSource = "qyl.feedback.source";

    public static class FeedbackSourceValues
    {
        public const string Api = "api";
        public const string Dashboard = "dashboard";
        public const string Mcp = "mcp";
        public const string Widget = "widget";
    }

    public const string FixRunId = "qyl.fix_run.id";

    public const string FixRunStatus = "qyl.fix_run.status";

    public static class FixRunStatusValues
    {
        public const string Failed = "failed";
        public const string Pending = "pending";
        public const string Rejected = "rejected";
        public const string Running = "running";
        public const string Succeeded = "succeeded";
    }

    public const string FixRunTrigger = "qyl.fix_run.trigger";

    public static class FixRunTriggerValues
    {
        public const string Automatic = "automatic";
        public const string Manual = "manual";
    }

    public const string IssueId = "qyl.issue.id";

    public const string IssueSeverity = "qyl.issue.severity";

    public static class IssueSeverityValues
    {
        public const string Critical = "critical";
        public const string High = "high";
        public const string Low = "low";
        public const string Medium = "medium";
    }

    public const string IssueStatus = "qyl.issue.status";

    public static class IssueStatusValues
    {
        public const string Ignored = "ignored";
        public const string Open = "open";
        public const string Resolved = "resolved";
    }

    public const string ProjectId = "qyl.project.id";

    public const string ProjectName = "qyl.project.name";

    public const string ReleaseChannel = "qyl.release.channel";

    public static class ReleaseChannelValues
    {
        public const string Beta = "beta";
        public const string Canary = "canary";
        public const string Preview = "preview";
        public const string Stable = "stable";
    }

    public const string ReleaseCommitSha = "qyl.release.commit_sha";

    public const string ReleaseEnvironment = "qyl.release.environment";

    public const string ReleaseVersion = "qyl.release.version";

    public const string RunId = "qyl.run.id";

    public const string RunKind = "qyl.run.kind";

    public static class RunKindValues
    {
        public const string Autofix = "autofix";
        public const string Review = "review";
        public const string Triage = "triage";
    }

    public const string RunStatus = "qyl.run.status";

    public static class RunStatusValues
    {
        public const string Failed = "failed";
        public const string Pending = "pending";
        public const string Running = "running";
        public const string Succeeded = "succeeded";
    }

    public const string StorageSize = "qyl.storage.size";

    public const string TeamId = "qyl.team.id";

    public const string TeamName = "qyl.team.name";

    public const string TriageCategory = "qyl.triage.category";

    public const string TriageId = "qyl.triage.id";

    public const string TriageScore = "qyl.triage.score";

}