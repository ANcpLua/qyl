namespace Qyl.Collector.Ingestion.Configuration;

/// <summary>
/// The configuration keys the Collector's options bind, each also its environment variable name.
/// The options types name them in <see cref="Microsoft.Extensions.Configuration.ConfigurationKeyNameAttribute"/>
/// and in their validation messages, so a key exists once.
/// </summary>
internal static class CollectorConfigurationKeys
{
    public const string BindAddress = "QYL_BIND_ADDRESS";
    public const string Port = "QYL_PORT";
    public const string PlatformPort = "PORT";
    public const string OtlpPort = "QYL_OTLP_PORT";
    public const string GrpcPort = "QYL_GRPC_PORT";
    public const string CorsAllowedOrigins = "QYL_OTLP_CORS_ALLOWED_ORIGINS";
    public const string CorsAllowedHeaders = "QYL_OTLP_CORS_ALLOWED_HEADERS";
    public const string AuthMode = "QYL_OTLP_AUTH_MODE";
    public const string ProjectKeys = "QYL_OTLP_PROJECT_KEYS";
    public const string RetentionDays = "QYL_RETENTION_DAYS";
    public const string RetentionIntervalMinutes = "QYL_RETENTION_INTERVAL_MINUTES";
    public const string StorageMinimumFreeMegabytes = "QYL_STORAGE_MIN_FREE_MB";
    public const string DataPath = "QYL_DATA_PATH";
    public const string DatabaseMemoryLimit = "QYL_DB_MEMORY_LIMIT";
    public const string DatabaseThreads = "QYL_DB_THREADS";
    public const string DatabaseTempDirectory = "QYL_DB_TEMP_DIR";
    public const string AnalysisSigningKey = "QYL_ANALYSIS_SIGNING_KEY";
}
