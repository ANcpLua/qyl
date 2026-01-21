// =============================================================================
// AUTO-GENERATED FILE - DO NOT EDIT
// =============================================================================
//     Source:    /Users/ancplua/qyl/core/openapi/openapi.yaml
//     Generated: 2026-01-16T09:00:34.9348970+00:00
//     Enumeration types
// =============================================================================

#nullable enable

namespace Qyl.Enums
{
    /// <summary>Aggregation functions</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AggregationFunction>))]
    public enum AggregationFunction
    {
        [System.Runtime.Serialization.EnumMember(Value = "count")]
        Count = 0,
        [System.Runtime.Serialization.EnumMember(Value = "sum")]
        Sum = 1,
        [System.Runtime.Serialization.EnumMember(Value = "avg")]
        Avg = 2,
        [System.Runtime.Serialization.EnumMember(Value = "min")]
        Min = 3,
        [System.Runtime.Serialization.EnumMember(Value = "max")]
        Max = 4,
        [System.Runtime.Serialization.EnumMember(Value = "p50")]
        P50 = 5,
        [System.Runtime.Serialization.EnumMember(Value = "p90")]
        P90 = 6,
        [System.Runtime.Serialization.EnumMember(Value = "p95")]
        P95 = 7,
        [System.Runtime.Serialization.EnumMember(Value = "p99")]
        P99 = 8,
        [System.Runtime.Serialization.EnumMember(Value = "count_distinct")]
        CountDistinct = 9,
    }

    /// <summary>Aggregation temporality for metrics</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<AggregationTemporality>))]
    public enum AggregationTemporality
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        _2 = 2,
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ApiVersions>))]
    public enum ApiVersions
    {
        [System.Runtime.Serialization.EnumMember(Value = "2024-01-01")]
        _20240101 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "2024-06-01")]
        _20240601 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2025-01-01")]
        _20250101 = 2,
    }

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

    /// <summary>Cloud provider types</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<CloudProvider>))]
    public enum CloudProvider
    {
        [System.Runtime.Serialization.EnumMember(Value = "alibaba_cloud")]
        AlibabaCloud = 0,
        [System.Runtime.Serialization.EnumMember(Value = "aws")]
        Aws = 1,
        [System.Runtime.Serialization.EnumMember(Value = "azure")]
        Azure = 2,
        [System.Runtime.Serialization.EnumMember(Value = "gcp")]
        Gcp = 3,
        [System.Runtime.Serialization.EnumMember(Value = "heroku")]
        Heroku = 4,
        [System.Runtime.Serialization.EnumMember(Value = "ibm_cloud")]
        IbmCloud = 5,
        [System.Runtime.Serialization.EnumMember(Value = "tencent_cloud")]
        TencentCloud = 6,
    }

    /// <summary>Data point flags</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DataPointFlags>))]
    public enum DataPointFlags
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
    }

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

    /// <summary>Device types</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DeviceType>))]
    public enum DeviceType
    {
        [System.Runtime.Serialization.EnumMember(Value = "desktop")]
        Desktop = 0,
        [System.Runtime.Serialization.EnumMember(Value = "mobile")]
        Mobile = 1,
        [System.Runtime.Serialization.EnumMember(Value = "tablet")]
        Tablet = 2,
        [System.Runtime.Serialization.EnumMember(Value = "tv")]
        Tv = 3,
        [System.Runtime.Serialization.EnumMember(Value = "console")]
        Console = 4,
        [System.Runtime.Serialization.EnumMember(Value = "wearable")]
        Wearable = 5,
        [System.Runtime.Serialization.EnumMember(Value = "iot")]
        Iot = 6,
        [System.Runtime.Serialization.EnumMember(Value = "bot")]
        Bot = 7,
        [System.Runtime.Serialization.EnumMember(Value = "unknown")]
        Unknown = 8,
    }

    /// <summary>DORA performance levels</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DoraPerformanceLevel>))]
    public enum DoraPerformanceLevel
    {
        [System.Runtime.Serialization.EnumMember(Value = "elite")]
        Elite = 0,
        [System.Runtime.Serialization.EnumMember(Value = "high")]
        High = 1,
        [System.Runtime.Serialization.EnumMember(Value = "medium")]
        Medium = 2,
        [System.Runtime.Serialization.EnumMember(Value = "low")]
        Low = 3,
    }

    /// <summary>High-level error categories</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorCategory>))]
    public enum ErrorCategory
    {
        [System.Runtime.Serialization.EnumMember(Value = "client")]
        Client = 0,
        [System.Runtime.Serialization.EnumMember(Value = "server")]
        Server = 1,
        [System.Runtime.Serialization.EnumMember(Value = "network")]
        Network = 2,
        [System.Runtime.Serialization.EnumMember(Value = "timeout")]
        Timeout = 3,
        [System.Runtime.Serialization.EnumMember(Value = "validation")]
        Validation = 4,
        [System.Runtime.Serialization.EnumMember(Value = "authentication")]
        Authentication = 5,
        [System.Runtime.Serialization.EnumMember(Value = "authorization")]
        Authorization = 6,
        [System.Runtime.Serialization.EnumMember(Value = "rate_limit")]
        RateLimit = 7,
        [System.Runtime.Serialization.EnumMember(Value = "not_found")]
        NotFound = 8,
        [System.Runtime.Serialization.EnumMember(Value = "conflict")]
        Conflict = 9,
        [System.Runtime.Serialization.EnumMember(Value = "internal")]
        Internal = 10,
        [System.Runtime.Serialization.EnumMember(Value = "external")]
        External = 11,
        [System.Runtime.Serialization.EnumMember(Value = "database")]
        Database = 12,
        [System.Runtime.Serialization.EnumMember(Value = "configuration")]
        Configuration = 13,
        [System.Runtime.Serialization.EnumMember(Value = "unknown")]
        Unknown = 14,
    }

    /// <summary>Error tracking status</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorStatus>))]
    public enum ErrorStatus
    {
        [System.Runtime.Serialization.EnumMember(Value = "new")]
        New = 0,
        [System.Runtime.Serialization.EnumMember(Value = "acknowledged")]
        Acknowledged = 1,
        [System.Runtime.Serialization.EnumMember(Value = "in_progress")]
        InProgress = 2,
        [System.Runtime.Serialization.EnumMember(Value = "resolved")]
        Resolved = 3,
        [System.Runtime.Serialization.EnumMember(Value = "ignored")]
        Ignored = 4,
        [System.Runtime.Serialization.EnumMember(Value = "regressed")]
        Regressed = 5,
        [System.Runtime.Serialization.EnumMember(Value = "wont_fix")]
        WontFix = 6,
    }

    /// <summary>Error trend</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ErrorTrend>))]
    public enum ErrorTrend
    {
        [System.Runtime.Serialization.EnumMember(Value = "increasing")]
        Increasing = 0,
        [System.Runtime.Serialization.EnumMember(Value = "decreasing")]
        Decreasing = 1,
        [System.Runtime.Serialization.EnumMember(Value = "stable")]
        Stable = 2,
        [System.Runtime.Serialization.EnumMember(Value = "spike")]
        Spike = 3,
    }

    /// <summary>Exception status</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ExceptionStatus>))]
    public enum ExceptionStatus
    {
        [System.Runtime.Serialization.EnumMember(Value = "new")]
        New = 0,
        [System.Runtime.Serialization.EnumMember(Value = "investigating")]
        Investigating = 1,
        [System.Runtime.Serialization.EnumMember(Value = "in_progress")]
        InProgress = 2,
        [System.Runtime.Serialization.EnumMember(Value = "resolved")]
        Resolved = 3,
        [System.Runtime.Serialization.EnumMember(Value = "ignored")]
        Ignored = 4,
        [System.Runtime.Serialization.EnumMember(Value = "regressed")]
        Regressed = 5,
    }

    /// <summary>Exception trend</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<ExceptionTrend>))]
    public enum ExceptionTrend
    {
        [System.Runtime.Serialization.EnumMember(Value = "up")]
        Up = 0,
        [System.Runtime.Serialization.EnumMember(Value = "down")]
        Down = 1,
        [System.Runtime.Serialization.EnumMember(Value = "stable")]
        Stable = 2,
    }

    /// <summary>Filter operators</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<FilterOperator>))]
    public enum FilterOperator
    {
        [System.Runtime.Serialization.EnumMember(Value = "eq")]
        Eq = 0,
        [System.Runtime.Serialization.EnumMember(Value = "neq")]
        Neq = 1,
        [System.Runtime.Serialization.EnumMember(Value = "contains")]
        Contains = 2,
        [System.Runtime.Serialization.EnumMember(Value = "starts_with")]
        StartsWith = 3,
        [System.Runtime.Serialization.EnumMember(Value = "ends_with")]
        EndsWith = 4,
        [System.Runtime.Serialization.EnumMember(Value = "regex")]
        Regex = 5,
        [System.Runtime.Serialization.EnumMember(Value = "gt")]
        Gt = 6,
        [System.Runtime.Serialization.EnumMember(Value = "gte")]
        Gte = 7,
        [System.Runtime.Serialization.EnumMember(Value = "lt")]
        Lt = 8,
        [System.Runtime.Serialization.EnumMember(Value = "lte")]
        Lte = 9,
        [System.Runtime.Serialization.EnumMember(Value = "in")]
        In = 10,
        [System.Runtime.Serialization.EnumMember(Value = "not_in")]
        NotIn = 11,
        [System.Runtime.Serialization.EnumMember(Value = "exists")]
        Exists = 12,
        [System.Runtime.Serialization.EnumMember(Value = "not_exists")]
        NotExists = 13,
    }

    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HealthStatus>))]
    public enum HealthStatus
    {
        [System.Runtime.Serialization.EnumMember(Value = "healthy")]
        Healthy = 0,
        [System.Runtime.Serialization.EnumMember(Value = "degraded")]
        Degraded = 1,
        [System.Runtime.Serialization.EnumMember(Value = "unhealthy")]
        Unhealthy = 2,
    }

    /// <summary>Host architecture types</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<HostArch>))]
    public enum HostArch
    {
        [System.Runtime.Serialization.EnumMember(Value = "amd64")]
        Amd64 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "arm32")]
        Arm32 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "arm64")]
        Arm64 = 2,
        [System.Runtime.Serialization.EnumMember(Value = "ia64")]
        Ia64 = 3,
        [System.Runtime.Serialization.EnumMember(Value = "ppc32")]
        Ppc32 = 4,
        [System.Runtime.Serialization.EnumMember(Value = "ppc64")]
        Ppc64 = 5,
        [System.Runtime.Serialization.EnumMember(Value = "s390x")]
        S390x = 6,
        [System.Runtime.Serialization.EnumMember(Value = "x86")]
        X86 = 7,
    }

    /// <summary>Log ordering options</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LogOrderBy>))]
    public enum LogOrderBy
    {
        [System.Runtime.Serialization.EnumMember(Value = "timestamp_asc")]
        TimestampAsc = 0,
        [System.Runtime.Serialization.EnumMember(Value = "timestamp_desc")]
        TimestampDesc = 1,
        [System.Runtime.Serialization.EnumMember(Value = "severity_asc")]
        SeverityAsc = 2,
        [System.Runtime.Serialization.EnumMember(Value = "severity_desc")]
        SeverityDesc = 3,
    }

    /// <summary>Log pattern trend</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<LogPatternTrend>))]
    public enum LogPatternTrend
    {
        [System.Runtime.Serialization.EnumMember(Value = "increasing")]
        Increasing = 0,
        [System.Runtime.Serialization.EnumMember(Value = "decreasing")]
        Decreasing = 1,
        [System.Runtime.Serialization.EnumMember(Value = "stable")]
        Stable = 2,
        [System.Runtime.Serialization.EnumMember(Value = "new")]
        New = 3,
        [System.Runtime.Serialization.EnumMember(Value = "spike")]
        Spike = 4,
    }

    /// <summary>Metric data type</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<MetricType>))]
    public enum MetricType
    {
        [System.Runtime.Serialization.EnumMember(Value = "gauge")]
        Gauge = 0,
        [System.Runtime.Serialization.EnumMember(Value = "sum")]
        Sum = 1,
        [System.Runtime.Serialization.EnumMember(Value = "histogram")]
        Histogram = 2,
        [System.Runtime.Serialization.EnumMember(Value = "exponential_histogram")]
        ExponentialHistogram = 3,
        [System.Runtime.Serialization.EnumMember(Value = "summary")]
        Summary = 4,
    }

    /// <summary>Operating system types</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<OsType>))]
    public enum OsType
    {
        [System.Runtime.Serialization.EnumMember(Value = "windows")]
        Windows = 0,
        [System.Runtime.Serialization.EnumMember(Value = "linux")]
        Linux = 1,
        [System.Runtime.Serialization.EnumMember(Value = "darwin")]
        Darwin = 2,
        [System.Runtime.Serialization.EnumMember(Value = "freebsd")]
        Freebsd = 3,
        [System.Runtime.Serialization.EnumMember(Value = "netbsd")]
        Netbsd = 4,
        [System.Runtime.Serialization.EnumMember(Value = "openbsd")]
        Openbsd = 5,
        [System.Runtime.Serialization.EnumMember(Value = "dragonflybsd")]
        Dragonflybsd = 6,
        [System.Runtime.Serialization.EnumMember(Value = "hpux")]
        Hpux = 7,
        [System.Runtime.Serialization.EnumMember(Value = "aix")]
        Aix = 8,
        [System.Runtime.Serialization.EnumMember(Value = "solaris")]
        Solaris = 9,
        [System.Runtime.Serialization.EnumMember(Value = "z_os")]
        ZOs = 10,
    }

    /// <summary>Session states</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SessionState>))]
    public enum SessionState
    {
        [System.Runtime.Serialization.EnumMember(Value = "active")]
        Active = 0,
        [System.Runtime.Serialization.EnumMember(Value = "idle")]
        Idle = 1,
        [System.Runtime.Serialization.EnumMember(Value = "ended")]
        Ended = 2,
        [System.Runtime.Serialization.EnumMember(Value = "timed_out")]
        TimedOut = 3,
        [System.Runtime.Serialization.EnumMember(Value = "invalidated")]
        Invalidated = 4,
    }

    /// <summary>Log severity number following OTel specification (1-24)</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityNumber>))]
    public enum SeverityNumber
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        _2 = 2,
        [System.Runtime.Serialization.EnumMember(Value = "3")]
        _3 = 3,
        [System.Runtime.Serialization.EnumMember(Value = "4")]
        _4 = 4,
        [System.Runtime.Serialization.EnumMember(Value = "5")]
        _5 = 5,
        [System.Runtime.Serialization.EnumMember(Value = "6")]
        _6 = 6,
        [System.Runtime.Serialization.EnumMember(Value = "7")]
        _7 = 7,
        [System.Runtime.Serialization.EnumMember(Value = "8")]
        _8 = 8,
        [System.Runtime.Serialization.EnumMember(Value = "9")]
        _9 = 9,
        [System.Runtime.Serialization.EnumMember(Value = "10")]
        _10 = 10,
        [System.Runtime.Serialization.EnumMember(Value = "11")]
        _11 = 11,
        [System.Runtime.Serialization.EnumMember(Value = "12")]
        _12 = 12,
        [System.Runtime.Serialization.EnumMember(Value = "13")]
        _13 = 13,
        [System.Runtime.Serialization.EnumMember(Value = "14")]
        _14 = 14,
        [System.Runtime.Serialization.EnumMember(Value = "15")]
        _15 = 15,
        [System.Runtime.Serialization.EnumMember(Value = "16")]
        _16 = 16,
        [System.Runtime.Serialization.EnumMember(Value = "17")]
        _17 = 17,
        [System.Runtime.Serialization.EnumMember(Value = "18")]
        _18 = 18,
        [System.Runtime.Serialization.EnumMember(Value = "19")]
        _19 = 19,
        [System.Runtime.Serialization.EnumMember(Value = "20")]
        _20 = 20,
        [System.Runtime.Serialization.EnumMember(Value = "21")]
        _21 = 21,
        [System.Runtime.Serialization.EnumMember(Value = "22")]
        _22 = 22,
        [System.Runtime.Serialization.EnumMember(Value = "23")]
        _23 = 23,
        [System.Runtime.Serialization.EnumMember(Value = "24")]
        _24 = 24,
    }

    /// <summary>Log severity text (human-readable)</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeverityText>))]
    public enum SeverityText
    {
        [System.Runtime.Serialization.EnumMember(Value = "TRACE")]
        TRACE = 0,
        [System.Runtime.Serialization.EnumMember(Value = "DEBUG")]
        DEBUG = 1,
        [System.Runtime.Serialization.EnumMember(Value = "INFO")]
        INFO = 2,
        [System.Runtime.Serialization.EnumMember(Value = "WARN")]
        WARN = 3,
        [System.Runtime.Serialization.EnumMember(Value = "ERROR")]
        ERROR = 4,
        [System.Runtime.Serialization.EnumMember(Value = "FATAL")]
        FATAL = 5,
    }

    /// <summary>Span kind describing the relationship between spans</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanKind>))]
    public enum SpanKind
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        _2 = 2,
        [System.Runtime.Serialization.EnumMember(Value = "3")]
        _3 = 3,
        [System.Runtime.Serialization.EnumMember(Value = "4")]
        _4 = 4,
        [System.Runtime.Serialization.EnumMember(Value = "5")]
        _5 = 5,
    }

    /// <summary>Span status code</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SpanStatusCode>))]
    public enum SpanStatusCode
    {
        [System.Runtime.Serialization.EnumMember(Value = "0")]
        _0 = 0,
        [System.Runtime.Serialization.EnumMember(Value = "1")]
        _1 = 1,
        [System.Runtime.Serialization.EnumMember(Value = "2")]
        _2 = 2,
    }

    /// <summary>Stream event types</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<StreamEventType>))]
    public enum StreamEventType
    {
        [System.Runtime.Serialization.EnumMember(Value = "traces")]
        Traces = 0,
        [System.Runtime.Serialization.EnumMember(Value = "spans")]
        Spans = 1,
        [System.Runtime.Serialization.EnumMember(Value = "logs")]
        Logs = 2,
        [System.Runtime.Serialization.EnumMember(Value = "metrics")]
        Metrics = 3,
        [System.Runtime.Serialization.EnumMember(Value = "exceptions")]
        Exceptions = 4,
        [System.Runtime.Serialization.EnumMember(Value = "deployments")]
        Deployments = 5,
        [System.Runtime.Serialization.EnumMember(Value = "all")]
        All = 6,
    }

    /// <summary>Telemetry SDK language</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TelemetrySdkLanguage>))]
    public enum TelemetrySdkLanguage
    {
        [System.Runtime.Serialization.EnumMember(Value = "cpp")]
        Cpp = 0,
        [System.Runtime.Serialization.EnumMember(Value = "dotnet")]
        Dotnet = 1,
        [System.Runtime.Serialization.EnumMember(Value = "erlang")]
        Erlang = 2,
        [System.Runtime.Serialization.EnumMember(Value = "go")]
        Go = 3,
        [System.Runtime.Serialization.EnumMember(Value = "java")]
        Java = 4,
        [System.Runtime.Serialization.EnumMember(Value = "nodejs")]
        Nodejs = 5,
        [System.Runtime.Serialization.EnumMember(Value = "php")]
        Php = 6,
        [System.Runtime.Serialization.EnumMember(Value = "python")]
        Python = 7,
        [System.Runtime.Serialization.EnumMember(Value = "ruby")]
        Ruby = 8,
        [System.Runtime.Serialization.EnumMember(Value = "rust")]
        Rust = 9,
        [System.Runtime.Serialization.EnumMember(Value = "swift")]
        Swift = 10,
        [System.Runtime.Serialization.EnumMember(Value = "webjs")]
        Webjs = 11,
    }

    /// <summary>Temporal relationship between errors</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TemporalRelationship>))]
    public enum TemporalRelationship
    {
        [System.Runtime.Serialization.EnumMember(Value = "concurrent")]
        Concurrent = 0,
        [System.Runtime.Serialization.EnumMember(Value = "precedes")]
        Precedes = 1,
        [System.Runtime.Serialization.EnumMember(Value = "follows")]
        Follows = 2,
        [System.Runtime.Serialization.EnumMember(Value = "unrelated")]
        Unrelated = 3,
    }

    /// <summary>Time bucket size for aggregations</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TimeBucket>))]
    public enum TimeBucket
    {
        [System.Runtime.Serialization.EnumMember(Value = "1m")]
        _1m = 0,
        [System.Runtime.Serialization.EnumMember(Value = "5m")]
        _5m = 1,
        [System.Runtime.Serialization.EnumMember(Value = "15m")]
        _15m = 2,
        [System.Runtime.Serialization.EnumMember(Value = "1h")]
        _1h = 3,
        [System.Runtime.Serialization.EnumMember(Value = "1d")]
        _1d = 4,
        [System.Runtime.Serialization.EnumMember(Value = "1w")]
        _1w = 5,
        [System.Runtime.Serialization.EnumMember(Value = "auto")]
        Auto = 6,
    }

    /// <summary>WebSocket message types</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<WebSocketMessageType>))]
    public enum WebSocketMessageType
    {
        [System.Runtime.Serialization.EnumMember(Value = "subscribe")]
        Subscribe = 0,
        [System.Runtime.Serialization.EnumMember(Value = "unsubscribe")]
        Unsubscribe = 1,
        [System.Runtime.Serialization.EnumMember(Value = "data")]
        Data = 2,
        [System.Runtime.Serialization.EnumMember(Value = "error")]
        Error = 3,
        [System.Runtime.Serialization.EnumMember(Value = "ack")]
        Ack = 4,
        [System.Runtime.Serialization.EnumMember(Value = "ping")]
        Ping = 5,
        [System.Runtime.Serialization.EnumMember(Value = "pong")]
        Pong = 6,
    }

}
