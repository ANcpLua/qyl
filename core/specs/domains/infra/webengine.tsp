// =============================================================================
// QYL v2.0 - Web Engine Semantic Conventions (OTel v1.38)
// =============================================================================
// Web server/engine resource attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Infra.WebEngine;

// =============================================================================
// Web Engine Resource Attributes
// =============================================================================

@doc("Web engine/server resource attributes")
model WebEngineAttributes {
  @doc("Web engine name")
  @encodedName("application/json", "webengine.name")
  name: string;

  @doc("Web engine version")
  @encodedName("application/json", "webengine.version")
  version?: SemVer;

  @doc("Web engine description")
  @encodedName("application/json", "webengine.description")
  description?: string;
}

// =============================================================================
// Web Engine Entity
// =============================================================================

@doc("Web engine entity for resource detection")
model WebEngineEntity {
  @doc("Engine name")
  @key
  name: string;

  @doc("Engine version")
  version?: SemVer;

  @doc("Engine type")
  type: WebEngineType;

  @doc("Engine description")
  description?: string;

  @doc("Runtime language")
  @encodedName("application/json", "runtime_language")
  runtimeLanguage?: RuntimeLanguage;
}

@doc("Web engine types")
enum WebEngineType {
  @doc("HTTP server")
  httpServer: "http_server",

  @doc("Application server")
  appServer: "app_server",

  @doc("Reverse proxy")
  reverseProxy: "reverse_proxy",

  @doc("Load balancer")
  loadBalancer: "load_balancer",

  @doc("API gateway")
  apiGateway: "api_gateway",

  @doc("CDN edge")
  cdnEdge: "cdn_edge",

  @doc("Serverless runtime")
  serverless: "serverless",

  @doc("Container runtime")
  containerRuntime: "container_runtime",
}

@doc("Runtime languages")
enum RuntimeLanguage {
  @doc("C/C++")
  cpp: "cpp",

  @doc(".NET (C#/F#)")
  dotnet: "dotnet",

  @doc("Go")
  go: "go",

  @doc("Java")
  java: "java",

  @doc("Node.js")
  nodejs: "nodejs",

  @doc("Python")
  python: "python",

  @doc("Ruby")
  ruby: "ruby",

  @doc("Rust")
  rust: "rust",

  @doc("PHP")
  php: "php",

  @doc("Elixir/Erlang")
  erlang: "erlang",
}

// =============================================================================
// Common Web Engines
// =============================================================================

@doc("Well-known web engines/servers")
enum WebEngineName {
  @doc("NGINX")
  nginx: "nginx",

  @doc("Apache HTTP Server")
  apache: "apache",

  @doc("Microsoft IIS")
  iis: "iis",

  @doc("Kestrel (.NET)")
  kestrel: "kestrel",

  @doc("Tomcat")
  tomcat: "tomcat",

  @doc("Jetty")
  jetty: "jetty",

  @doc("Express.js")
  express: "express",

  @doc("Fastify")
  fastify: "fastify",

  @doc("Gunicorn")
  gunicorn: "gunicorn",

  @doc("uWSGI")
  uwsgi: "uwsgi",

  @doc("Uvicorn")
  uvicorn: "uvicorn",

  @doc("Daphne")
  daphne: "daphne",

  @doc("Puma")
  puma: "puma",

  @doc("Unicorn")
  unicorn: "unicorn",

  @doc("Caddy")
  caddy: "caddy",

  @doc("Traefik")
  traefik: "traefik",

  @doc("HAProxy")
  haproxy: "haproxy",

  @doc("Envoy")
  envoy: "envoy",

  @doc("Cloudflare Workers")
  cloudflareWorkers: "cloudflare_workers",

  @doc("AWS Lambda")
  awsLambda: "aws_lambda",

  @doc("Vercel Edge")
  vercelEdge: "vercel_edge",

  @doc("Deno Deploy")
  denoDeploy: "deno_deploy",

  @doc("Bun")
  bun: "bun",

  @doc("Other")
  other: "other",
}

// =============================================================================
// Web Engine Metrics
// =============================================================================

@doc("Web engine request count metric")
model WebEngineRequestsMetric {
  @doc("Metric name")
  name: "webengine.requests";

  @doc("Metric unit")
  unit: "{request}";

  @doc("Web engine name")
  @encodedName("application/json", "webengine.name")
  engineName: string;

  @doc("HTTP method")
  @encodedName("application/json", "http.request.method")
  method?: string;

  @doc("Response status code")
  @encodedName("application/json", "http.response.status_code")
  statusCode?: int32;
}

@doc("Web engine active connections metric")
model WebEngineConnectionsMetric {
  @doc("Metric name")
  name: "webengine.connections";

  @doc("Metric unit")
  unit: "{connection}";

  @doc("Web engine name")
  @encodedName("application/json", "webengine.name")
  engineName: string;

  @doc("Connection state")
  state: ConnectionState;
}

@doc("Connection states")
enum ConnectionState {
  @doc("Active connection")
  active: "active",

  @doc("Idle connection")
  idle: "idle",

  @doc("Reading request")
  reading: "reading",

  @doc("Writing response")
  writing: "writing",

  @doc("Waiting for keepalive")
  waiting: "waiting",
}
