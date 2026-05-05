
#nullable enable

namespace QylAttr;

public static class ApiKey
{
    public const string Id = "qyl.api_key.id";
}

public static class Auth
{
    public const string InstanceId = "qyl.auth.instance_id";
    public const string KeycloakClaims = "qyl.auth.keycloak_claims";
}

public static class Capability
{
    public const string Id = "qyl.capability.id";
    public const string Kind = "qyl.capability.kind";
}

public static class CheckIn
{
    public const string DurationMs = "qyl.check_in.duration_ms";
    public const string MonitorSlug = "qyl.check_in.monitor_slug";
    public const string ScheduleCron = "qyl.check_in.schedule_cron";
    public const string ScheduleIntervalMinutes = "qyl.check_in.schedule_interval_minutes";
    public const string Status = "qyl.check_in.status";
}

public static class Duckdb
{
    public const string DroppedJobsTotal = "qyl.duckdb.dropped_jobs_total";
    public const string DroppedSpansTotal = "qyl.duckdb.dropped_spans_total";
}

public static class Feedback
{
    public const string ContactEmail = "qyl.feedback.contact_email";
    public const string EventId = "qyl.feedback.event_id";
    public const string Id = "qyl.feedback.id";
    public const string Source = "qyl.feedback.source";
}

public static class FixRun
{
    public const string Id = "qyl.fix_run.id";
    public const string Status = "qyl.fix_run.status";
    public const string Trigger = "qyl.fix_run.trigger";
}

public static class Issue
{
    public const string Id = "qyl.issue.id";
    public const string Severity = "qyl.issue.severity";
    public const string Status = "qyl.issue.status";
}

public static class Project
{
    public const string Id = "qyl.project.id";
    public const string Name = "qyl.project.name";
}

public static class Release
{
    public const string Channel = "qyl.release.channel";
    public const string CommitSha = "qyl.release.commit_sha";
    public const string Environment = "qyl.release.environment";
    public const string Version = "qyl.release.version";
}

public static class Run
{
    public const string Id = "qyl.run.id";
    public const string Kind = "qyl.run.kind";
    public const string Status = "qyl.run.status";
}

public static class Storage
{
    public const string Size = "qyl.storage.size";
}

public static class Team
{
    public const string Id = "qyl.team.id";
    public const string Name = "qyl.team.name";
}

public static class Triage
{
    public const string Category = "qyl.triage.category";
    public const string Id = "qyl.triage.id";
    public const string Score = "qyl.triage.score";
}
