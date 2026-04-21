// Copyright (c) 2025-2026 ancplua

namespace Qyl.Run;

/// <summary>
/// Every magic string and magic port qyl.run references routes through here. Follow the
/// <c>AddOptionsWithValidateOnStart&lt;T&gt;().BindConfiguration(T.SectionName)</c> pattern —
/// identifiers that show up in more than one place must be constants.
/// </summary>
public static class QylConstants
{
    /// <summary>Product metadata that drives the CLI banner + User-Agent headers.</summary>
    public static class Product
    {
        public const string Name = "qyl.run";
        public const string Banner = "qyl";
        public const string Version = "0.1.0";
        public const string UserAgent = $"{Name}/{Version}";
        public const string Tagline = "qyl distributed-app runner";
    }

    /// <summary>Default ports the CLI surfaces. Aligned with the Aspire dashboard defaults so
    /// muscle memory survives the migration.</summary>
    public static class Ports
    {
        public const int Dashboard = 18888;
        public const int OtlpGrpc = 4317;
        public const int OtlpHttp = 4318;
        public const int McpStreamable = 18891;
        public const int DynamicAllocation = 0;
    }

    /// <summary>Canonical resource kinds surfaced in the Spectre live table.</summary>
    public static class ResourceKinds
    {
        public const string Dashboard = "dashboard";
        public const string Collector = "collector";
        public const string Mcp = "mcp";
        public const string Loom = "loom";
        public const string Project = "project";
    }

    /// <summary>Deployment environment tags. Alignment with the Aspire + ASP.NET conventions.</summary>
    public static class Environments
    {
        public const string Dev = "dev";
        public const string Staging = "staging";
        public const string Prod = "prod";
    }

    /// <summary>Network binding defaults.</summary>
    public static class Network
    {
        public const string Loopback = "127.0.0.1";
        public const string LocalhostUrlTemplate = "http://{0}:{1}";
        public const string HttpScheme = "http";
    }

    /// <summary>Environment variables that children inherit. Matches the ASP.NET Core runtime
    /// contract so `dotnet run` subprocesses bind where we tell them.</summary>
    public static class Env
    {
        public const string AspNetCoreUrls = "ASPNETCORE_URLS";
        public const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";
        public const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
        public const string OtelServiceName = "OTEL_SERVICE_NAME";
        public const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    }

    /// <summary>HTTP contract for the runner's own surface.</summary>
    public static class Routes
    {
        public const string Health = "/health";
        public const string Runner = "/runner";
        public const string Fleet = "/api/v1/fleet";
        public const string ApiRoot = "/api/v1";
    }

    /// <summary>Named HttpClient pools.</summary>
    public static class HttpClients
    {
        public const string HealthProbe = "qyl-run-health";
        public const string FleetProxy = "qyl-run-proxy";
    }

    /// <summary>Key bindings shown in the CLI footer. Lowercase is equally accepted at runtime.</summary>
    public static class Keys
    {
        public const char Stop = 'S';
        public const char Restart = 'R';
        public const char Browser = 'B';
        public const char Help = 'H';
    }

    /// <summary>Child-process runtime defaults.</summary>
    public static class Orchestrator
    {
        public const string DotnetExecutable = "dotnet";
        public const string RunCommand = "run";
        public const string ProjectFlag = "--project";
        public const int HealthPollIntervalMs = 500;
        public const int StartupTimeoutSeconds = 60;
    }

    /// <summary>Logging event-id bands so LoggerMessage IDs stay unique.</summary>
    public static class LogEvents
    {
        public const int OrchestratorStarted = 1100;
        public const int ResourceStarting = 1101;
        public const int ResourceReady = 1102;
        public const int ResourceFailed = 1103;
        public const int ResourceStopped = 1104;
    }
}
