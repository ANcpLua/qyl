namespace qyl.collector.tests.Helpers;

/// <summary>
///     Shared constants for test data across all test classes.
/// </summary>
internal static class TestConstants
{
    // Database
    public const string InMemoryDb = ":memory:";
    public const int DefaultJobQueueCapacity = 100;
    public const int DefaultMaxConcurrentReads = 4;
    public const int DefaultMaxRetainedReadConnections = 8;

    // Processing delays (increased for reliable async processing)
    public const int SchemaInitDelayMs = 200;
    public const int SingleSpanProcessingDelayMs = 500;
    public const int BatchProcessingDelayMs = 500;
    public const int LargeBatchProcessingDelayMs = 800;
    public const int ArchiveProcessingDelayMs = 500;
    public const int ConcurrentReadDelayMs = 1000;

    // Session IDs
    public const string SessionDefault = "session-001";
    public const string SessionMultiple = "session-002";
    public const string SessionPool = "session-pool";
    public const string SessionFilters = "session-filters";
    public const string SessionStats = "session-stats";
    public const string SessionArchive = "session-archive";

    // Trace IDs
    public const string TraceDefault = "trace-001";
    public const string TraceDuplicate = "trace-003";
    public const string TraceNullable = "trace-004";
    public const string TraceHierarchy = "trace-005";
    public const string TraceNonExistent = "trace-nonexistent";
    public const string TraceDuration = "trace-duration";
    public const string TraceLarge = "trace-large";
    public const string TraceLease = "trace-lease";

    // Span IDs
    public const string SpanDefault = "span-001";
    public const string SpanDuplicate = "span-003";
    public const string SpanNullable = "span-004";
    public const string SpanRoot = "root";
    public const string SpanChild1 = "child1";
    public const string SpanChild2 = "child2";

    // Provider names
    public const string ProviderOpenAi = "openai";
    public const string ProviderAnthropic = "anthropic";

    // Model names
    public const string ModelGpt4 = "gpt-4";

    // Service names
    public const string ServiceDefault = "test-service";

    // Operation names
    public const string OperationDefault = "test.operation";
    public const string OperationMinimal = "minimal";
    public const string OperationTimed = "timed-operation";
    public const string OperationLargeData = "large-data";

    // Token counts
    public const long TokensInDefault = 50;
    public const long TokensOutDefault = 100;
    public const long TokensInSmall = 10;
    public const long TokensOutSmall = 20;

    // Cost values (double to match schema gen_ai_cost_usd)
    public const double CostDefault = 0.02;
    public const double CostSmall = 0.01;
    public const double CostMedium = 0.04;
    public const double CostLarge = 0.05;

    // Durations
    public const double DurationDefaultMs = 100;
    public const double DurationShortMs = 10;
    public const double DurationMediumMs = 50;
    public const double DurationPreciseMs = 123.45;

    // Batch sizes
    public const int BatchSizeSmall = 5;
    public const int BatchSizeMedium = 10;
    public const int BatchSizeLarge = 20;
    public const int ConcurrentReadCount = 8;

    // Archive settings
    public const int ArchiveDaysOld = 2;
    public const int ArchiveCutoffDays = 1;

    // Large data
    public const int LargeJsonPadding = 10000;

    // Expected values for assertions
    public static readonly string[] ExpectedArchivedTraces = ["trace-old1", "trace-old2"];
}