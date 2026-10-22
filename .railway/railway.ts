import { defineRailway, github, preserve, project, service, volume } from "railway/iac";

// Both qyl services of the shared "qyl" project. The project holds other resources this file
// does not manage, so it stays a named partial: a named partial deletes only what it owns.
export const partial = "qyl";

export default defineRailway(() => {
  const collectorVolume = volume("qyl-collector-volume", {
    region: "europe-west4-drams3a",
    sizeMB: 50000,
    allowOnlineResize: true,
    alerts: { usage: { "80": {}, "95": {}, "100": {} } },
  });

  const collector = service("qyl-collector", {
    // No push trigger since 2026-10-22: a push to main deploys nothing. A deploy is
    // serviceInstanceDeployV2 with a release-tag commit, or "Deploy Latest Commit".
    source: github("ANcpLua/qyl"),
    build: {
      builder: "DOCKERFILE",
      dockerfilePath: "services/qyl.collector/Dockerfile",
      buildEnvironment: "V3",
      // The Collector's build inputs. Watch patterns filter push-triggered deploys only, which are
      // off; keep them current so a returning trigger rebuilds on these paths alone. Include a new
      // top-level build input here.
      watchPatterns: [
        "services/qyl.collector*/**", "internal/**",
        "eng/MSBuild/**", "eng/scripts/publish-collector-aot.sh",
        "Directory.*", "Version.props", "global.json", "nuget.config",
        ".dockerignore", ".editorconfig", ".globalconfig",
      ],
    },
    deploy: {
      healthcheckPath: "/health",
      // The collector answers /health about a second after the volume mounts; 300 s (Railway's
      // default) leaves room for a slow start such as a long DuckDB WAL replay, so one never fails
      // the deploy. A crash at start still fails it: the healthcheck only waits for a 2xx.
      healthcheckTimeout: 300,
      runtime: "V2",
      limitOverride: { containers: { cpu: 24, memoryBytes: 24000000000 } },
      // Railway: "To prevent data corruption, we prevent multiple deployments from being active
      // and mounted to the same service. This means that there will be a small amount of downtime
      // when re-deploying a service that has a volume attached, even if there is a healthcheck
      // endpoint configured." (docs.railway.com/deployments/healthchecks). With one replica on
      // one volume the switch window is the honest minimum: no overlap, and 60 s for the old
      // deployment to drain on SIGTERM.
      overlapSeconds: 0,
      drainingSeconds: 60,
      useLegacyStacker: false,
      // Not declared on purpose: restartPolicyType ON_FAILURE, restartPolicyMaxRetries 10
      // and sleepApplication false are Railway's defaults, and requiredMountPath is
      // covered by volumeMounts below. Railway CLI 5.45.10 accepts but does not persist
      // those four fields, so declaring them leaves `railway config plan` permanently dirty.
      ipv6EgressEnabled: false,
    },
    replicas: { "europe-west4-drams3a": 1 },
    domains: ["api.qyl.at"],
    volumeMounts: { "/data": collectorVolume },
    // Values stay in Railway; the file only declares which variables exist.
    env: {
      QYL_ANALYSIS_SIGNING_KEY: preserve(),
      QYL_DATA_PATH: preserve(),
      QYL_GRPC_PORT: preserve(),
      QYL_OTLP_AUTH_MODE: preserve(),
      QYL_OTLP_PORT: preserve(),
      QYL_OTLP_PROJECT_KEYS: preserve(),
      QYL_PORT: preserve(),
      QYL_RETENTION_DAYS: preserve(),
    },
  });

  // Holds nothing the MCP server reads. It stays mounted so the server can delete the retired
  // MCP Events store (/data/mcp-events.json) at startup.
  const mcpVolume = volume("qyl-mcp-volume", {
    region: "europe-west4-drams3a",
    sizeMB: 1000,
    allowOnlineResize: true,
    alerts: { usage: { "80": {}, "95": {}, "100": {} } },
  });

  const mcp = service("qyl-mcp", {
    // No push trigger since 2026-10-22: a push to main deploys nothing. A deploy is
    // serviceInstanceDeployV2 with a release-tag commit, or "Deploy Latest Commit".
    // Railway pulls only services/qyl.mcp, which carries its generated contract module;
    // Railpack takes Bun from its package.json packageManager.
    source: github("ANcpLua/qyl", { rootDirectory: "services/qyl.mcp" }),
    build: {
      builder: "RAILPACK",
      buildCommand: "bun run build",
      watchPatterns: ["services/qyl.mcp/**"],
    },
    deploy: {
      startCommand: "bun dist/main.js",
      healthcheckPath: "/healthz",
      healthcheckTimeout: 30,
    },
    replicas: { "europe-west4-drams3a": 1 },
    domains: ["mcp.qyl.at"],
    volumeMounts: { "/data": mcpVolume },
    // Values stay in Railway; the file only declares which variables exist.
    env: {
      MCP_ALLOWED_HOSTS: preserve(),
      MCP_ALLOWED_ORIGIN_HOSTS: preserve(),
      MCP_BIND_HOST: preserve(),
      MCP_COLLECTOR_PROJECTS: preserve(),
      MCP_PUBLIC_URL: preserve(),
      NODE_ENV: preserve(),
      // The OpenAI plugin portal's domain-verification token, set when the portal shows it.
      OPENAI_APPS_CHALLENGE: preserve(),
      QYL_API_KEY: preserve(),
      QYL_COLLECTOR_URL: preserve(),
    },
  });

  return project("qyl", { resources: [collector, collectorVolume, mcp, mcpVolume] });
});
