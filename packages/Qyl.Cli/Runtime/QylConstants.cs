namespace Qyl.Cli.Runtime;

internal static class QylConstants
{
    public static class Product
    {
        public const string Banner = "qyl";
        public const string Version = BuildVersion.ProductVersion;
        public const string Tagline = "qyl distributed-app runner";

        /// <summary>
        /// The <c>service.name</c> the runner process reports for itself. The collectors it launches
        /// report their resource names (<c>collector</c>, <c>diagnostics</c>); this one names the
        /// process that launches them.
        /// </summary>
        public const string RunnerServiceName = "qyl-runner";
    }

    public static class Ports
    {
        public const int RunnerApi = 18889;
        public const int DynamicAllocation = OrchestrationDefaults.Ports.DynamicAllocation;
    }

    public static class Environments
    {
        public const string Dev = "dev";
    }

    public static class Network
    {
        public const string Loopback = OrchestrationDefaults.Network.Loopback;
        public const string LocalhostUrlTemplate = OrchestrationDefaults.Network.LocalhostUrlTemplate;
        public const string HttpScheme = "http";
    }

    public static class Env
    {
        public const string AspNetCoreUrls = OrchestrationDefaults.Env.AspNetCoreUrls;
        public const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        public const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
        public const string OtelServiceName = "OTEL_SERVICE_NAME";
        public const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
        public const string OtelExporterOtlpProtocol = "OTEL_EXPORTER_OTLP_PROTOCOL";
        public const string QylPort = "QYL_PORT";
        public const string QylOtlpPort = "QYL_OTLP_PORT";
        public const string QylGrpcPort = "QYL_GRPC_PORT";
        public const string QylBindAddress = "QYL_BIND_ADDRESS";
        public const string QylDataPath = "QYL_DATA_PATH";
        public const string QylOtlpAuthMode = "QYL_OTLP_AUTH_MODE";
    }

    public static class Collector
    {
        public const int DefaultOtlpHttpPort = 4318;
        public const int DefaultGrpcPort = 4317;
        public const string OtlpHttpProtobuf = "http/protobuf";
        public const string UnsecuredAuthMode = "Unsecured";
        public const string DataPathTemplate = "qyl.{0}.duckdb";

        /// <summary>
        /// Where collector children keep their DuckDB files. A fixed per-user home keeps
        /// telemetry out of whatever directory the operator happened to run `qyl up` from —
        /// a relative default would litter git repositories with growing database files.
        /// </summary>
        public static string DefaultDataHome =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qyl");
    }

    public static class Routes
    {
        public const string Health = OrchestrationDefaults.Routes.Health;
        public const string Runner = "/runner";
    }

    public static class HttpClients
    {
        public const string HealthProbe = OrchestrationDefaults.HttpClients.HealthProbe;
    }

    public static class Keys
    {
        public const char Stop = 'S';
        public const char Restart = 'R';
        public const char Help = 'H';
        public const char Browser = 'B';
    }

    public static class Orchestrator
    {
        public const string DotnetExecutable = "dotnet";
        public const int HealthPollIntervalMs = OrchestrationDefaults.Orchestrator.HealthPollIntervalMs;
        public const int HealthProbeAttemptTimeoutSeconds = OrchestrationDefaults.Orchestrator.HealthProbeAttemptTimeoutSeconds;
        public const int StartupTimeoutSeconds = OrchestrationDefaults.Orchestrator.StartupTimeoutSeconds;
        public const int MaxRestarts = OrchestrationDefaults.Orchestrator.MaxRestarts;
    }

    public static class LogEvents
    {
        public const int OrchestratorStarted = OrchestrationDefaults.LogEvents.OrchestratorStarted;
        public const int ResourceReady = OrchestrationDefaults.LogEvents.ResourceReady;
        public const int ResourceFailed = OrchestrationDefaults.LogEvents.ResourceFailed;
        public const int ResourceRestarting = OrchestrationDefaults.LogEvents.ResourceRestarting;
        public const int ChildStdout = OrchestrationDefaults.LogEvents.ChildStdout;
        public const int ChildStderr = OrchestrationDefaults.LogEvents.ChildStderr;
        public const int RunnerApiListening = 1107;
        public const int RunnerApiBindFailed = 1108;
        public const int RunnerApiRequestFailed = 1109;
        public const int ResourceUserRestart = OrchestrationDefaults.LogEvents.ResourceUserRestart;
    }
}
