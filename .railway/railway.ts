import { defineRailway, github, preserve, project, service, volume } from "railway/iac";

// This repository owns only the qyl-collector service and its volume. The
// qyl-mcp service is owned by ANcpLua/qyl.mcp, so each repo manages a named
// partial of the shared "qyl" project instead of one whole-project file.
export const partial = "qyl-collector";

export default defineRailway(() => {
  const dataVolume = volume("qyl-collector-volume", {
    region: "europe-west4-drams3a",
    sizeMB: 50000,
    allowOnlineResize: true,
    alerts: { usage: { "80": {}, "95": {}, "100": {} } },
  });

  const collector = service("qyl-collector", {
    source: github("ANcpLua/qyl", { checkSuites: true }),
    build: {
      builder: "DOCKERFILE",
      dockerfilePath: "services/qyl.collector/Dockerfile",
      buildEnvironment: "V3",
    },
    deploy: {
      healthcheckPath: "/health",
      healthcheckTimeout: 60,
      runtime: "V2",
      limitOverride: { containers: { cpu: 24, memoryBytes: 24000000000 } },
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
    volumeMounts: { "/data": dataVolume },
    // Values stay in Railway; the file only declares which variables exist.
    env: {
      QYL_DATA_PATH: preserve(),
      QYL_GRPC_PORT: preserve(),
      QYL_OTLP_AUTH_MODE: preserve(),
      QYL_OTLP_PORT: preserve(),
      QYL_OTLP_PRIMARY_API_KEY: preserve(),
      QYL_PORT: preserve(),
      QYL_RETENTION_DAYS: preserve(),
    },
  });

  return project("qyl", { resources: [collector, dataVolume] });
});
