
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
        public const int RunnerApi = 18888;
        public const int DynamicAllocation = 0;
    }

    public static class ResourceKinds
    {
        public const string Collector = "collector";
        public const string Project = "project";
    }

    public static class Environments
    {
        public const string Dev = "dev";
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
    }

    public static class Routes
    {
        public const string Health = "/health";
        public const string Runner = "/runner";
    }

    public static class HttpClients
    {
        public const string HealthProbe = "qyl-run-health";
    }

    public static class Keys
    {
        public const char Stop = 'S';
        public const char Restart = 'R';
        public const char Help = 'H';
    }

    public static class Orchestrator
    {
        public const string DotnetExecutable = "dotnet";
        public const string RunCommand = "run";
        public const string ProjectFlag = "--project";
        public const int HealthPollIntervalMs = 500;
        public const int HealthProbeAttemptTimeoutSeconds = 5;
        public const int StartupTimeoutSeconds = 60;
        public const int MaxRestarts = 3;
    }

    public static class LogEvents
    {
        public const int OrchestratorStarted = 1100;
        public const int ResourceStarting = 1101;
        public const int ResourceReady = 1102;
        public const int ResourceFailed = 1103;
        public const int ResourceStopped = 1104;
        public const int ResourceRestarting = 1113;
        public const int ChildStdout = 1105;
        public const int ChildStderr = 1106;
        public const int RunnerApiListening = 1107;
        public const int RunnerApiBindFailed = 1108;
        public const int RunnerApiRequestFailed = 1109;
        public const int ResourceUserRestart = 1114;
    }
}
