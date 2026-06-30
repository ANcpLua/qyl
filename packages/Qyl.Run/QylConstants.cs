
namespace Qyl.Run;

public static class QylConstants
{
    public static class Product
    {
        public const string Name = "qyl.run";
        public const string Banner = "qyl";
        public const string Version = "0.1.0";
        public const string UserAgent = $"{Name}/{Version}";
        public const string Tagline = "qyl distributed-app runner";
    }

    public static class Ports
    {
        public const int Dashboard = 18888;
        public const int OtlpGrpc = 4317;
        public const int OtlpHttp = 4318;
        public const int DynamicAllocation = 0;
    }

    public static class ResourceKinds
    {
        public const string Dashboard = "dashboard";
        public const string Collector = "collector";
        public const string Project = "project";
    }

    public static class Environments
    {
        public const string Dev = "dev";
        public const string Staging = "staging";
        public const string Prod = "prod";
    }

    public static class Network
    {
        public const string Loopback = "127.0.0.1";
        public const string LocalhostUrlTemplate = "http://{0}:{1}";
        public const string HttpScheme = "http";
    }

    public static class Env
    {
        public const string AspNetCoreUrls = "ASPNETCORE_URLS";
        public const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        public const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
        public const string OtelServiceName = "OTEL_SERVICE_NAME";
        public const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    }

    public static class Routes
    {
        public const string Health = "/health";
        public const string Runner = "/runner";
        public const string Fleet = "/api/v1/fleet";
        public const string ApiRoot = "/api/v1";
    }

    public static class HttpClients
    {
        public const string HealthProbe = "qyl-run-health";
        public const string FleetProxy = "qyl-run-proxy";
    }

    public static class Keys
    {
        public const char Stop = 'S';
        public const char Restart = 'R';
        public const char Browser = 'B';
        public const char Help = 'H';
    }

    public static class Orchestrator
    {
        public const string DotnetExecutable = "dotnet";
        public const string RunCommand = "run";
        public const string ProjectFlag = "--project";
        public const int HealthPollIntervalMs = 500;
        public const int StartupTimeoutSeconds = 60;
    }

    public static class LogEvents
    {
        public const int OrchestratorStarted = 1100;
        public const int ResourceStarting = 1101;
        public const int ResourceReady = 1102;
        public const int ResourceFailed = 1103;
        public const int ResourceStopped = 1104;
        public const int ChildStdout = 1105;
        public const int ChildStderr = 1106;
    }
}
