namespace Qyl.Orchestration.Internal;

internal static class OrchestrationDefaults
{
    internal static class Ports
    {
        public const int DynamicAllocation = 0;
    }

    internal static class Network
    {
        public const string Loopback = "127.0.0.1";
        public const string LocalhostUrlTemplate = "http://{0}:{1}";
    }

    internal static class Env
    {
        public const string AspNetCoreUrls = "ASPNETCORE_URLS";
    }

    internal static class Routes
    {
        public const string Health = "/health";
    }

    internal static class HttpClients
    {
        public const string HealthProbe = "qyl-run-health";
    }

    internal static class Orchestrator
    {
        public const int HealthPollIntervalMs = 500;
        public const int HealthProbeAttemptTimeoutSeconds = 5;
        public const int StartupTimeoutSeconds = 60;
        public const int MaxRestarts = 3;
    }

    internal static class LogEvents
    {
        public const int OrchestratorStarted = 1100;
        public const int ResourceReady = 1102;
        public const int ResourceFailed = 1103;
        public const int ResourceRestarting = 1113;
        public const int ChildStdout = 1105;
        public const int ChildStderr = 1106;
        public const int ResourceUserRestart = 1114;
    }
}

internal sealed class OrchestratorOptions
{
    public int StartupTimeoutSeconds { get; init; } = OrchestrationDefaults.Orchestrator.StartupTimeoutSeconds;
    public int MaxRestarts { get; init; } = OrchestrationDefaults.Orchestrator.MaxRestarts;
}
