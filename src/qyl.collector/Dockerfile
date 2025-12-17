⏺ Paperless.Telemetry Analysis

  🟢 Potenziell nützlich für qyl

  | Component                   | Beschreibung                               | Nutzen für qyl                          |
  |-----------------------------|--------------------------------------------|-----------------------------------------|
  | SchemaCore.cs               | OTel Schema YAML Parser                    | ⭐⭐⭐ Könnte SchemaNormalizer ersetzen |
  | SchemaDiffer                | Berechnet transitive Renames (A→B→C = A→C) | ⭐⭐⭐ Automatische Migration           |
  | SchemaTransformationGraph   | Effizientes Attribute-Lookup               | ⭐⭐ Performance                        |
  | DeprecatedAttributeAnalyzer | Roslyn Analyzer für deprecated attrs       | ⭐ Haben wir schon (QYL0002)            |

  Was SchemaCore macht

  // Parst offizielle OTel Schema YAML
  var parser = new SchemaParser();
  var schema = parser.Parse(yamlContent);

  // Berechnet alle Renames von Version X zu Y
  var differ = new SchemaDiffer(schema);
  var renames = differ.GetRenames(from: "1.13.0", to: "1.38.0");

  // Löst Attribute-Namen automatisch auf
  var graph = new SchemaTransformationGraph(schema, targetVersion: "1.38.0");
  string newName = graph.ResolveAttributeName("http.method"); // → "http.request.method"

  Empfehlung

  | Option | Beschreibung                                                                       |
  |--------|------------------------------------------------------------------------------------|
  | A      | SchemaCore nach qyl.collector/Ingestion/ kopieren, SchemaNormalizer darauf umbauen |
  | B      | Ignorieren, hardcoded mappings behalten (einfacher)                                |
  | C      | Als separates NuGet Package extrahieren (OTel.Schema.NET)                          |

  Meine Empfehlung: Option B für jetzt (MVP). SchemaCore ist nice-to-have aber nicht kritisch. Der hardcoded SchemaNormalizer funktioniert für die paar gen_ai Attribute.

  ---
  Rest von Paperless.Telemetry: Dashboard.TestClient, lol.Cli - sieht nach Experimenten aus, nicht nützlich.

  Fazit: Kannst du löschen, außer du willst SchemaCore irgendwann für automatische OTel-Migration nutzen.

> Python development environment loaded (Python 3.14)
ancplua@Alexanders-MacBook-Air Paperless.Telemetry % tree
 .
├──  docs
│   ├──  architecture
│   │   └──  decisions
│   │       ├──  0001-custom-telemetry-dashboard.md
│   │       ├──  0002-zero-code-instrumentation.md
│   │       ├──  0003-semantic-convention-normalization.md
│   │       ├──  0004-polyglot-support.md
│   │       ├──  0005-monorepo-structure.md
│   │       ├── 󰂺 README.md
│   │       └──  template.md
│   └──  workflows
│       ├──  PHASE-1-ARTIFACT-BACKEND.md
│       ├──  PHASE-2-ARTIFACT-RENDERERS.md
│       ├──  PHASE-3-ARTIFACT-PRODUCERS.md
│       └──  PHASE-4-INTEGRATION-POLISH.md
├──  scripts
│   ├──  smoke-test.sh
│   └──  verify-lol-e2e.sh
├── 󰣞 src
│   ├──  Dashboard.Receiver
│   │   ├──  Abstractions
│   │   │   ├── 󰌛 IServiceRegistry.cs
│   │   │   ├── 󰌛 ITelemetryBroadcaster.cs
│   │   │   ├── 󰌛 ITelemetryStore.cs
│   │   │   └── 󰌛 ITraceAggregator.cs
│   │   ├──  Api
│   │   │   ├──  Contracts
│   │   │   └── 󰌛 TelemetryEndpoints.cs
│   │   ├──  Configuration
│   │   │   └── 󰌛 ReceiverOptions.cs
│   │   ├──  Extensions
│   │   │   └── 󰌛 ServiceCollectionExtensions.cs
│   │   ├──  Properties
│   │   │   └──  launchSettings.json
│   │   ├──  Protocol
│   │   │   ├── 󰌛 IOtlpConverter.cs
│   │   │   ├── 󰌛 OtlpConverter.cs
│   │   │   └── 󰌛 OtlpHttpHandler.cs
│   │   ├──  Protos
│   │   │   └──  opentelemetry
│   │   ├──  SemanticConventions
│   │   │   ├── 󰌛 LogAttributes.cs
│   │   │   ├── 󰌛 MetricNames.cs
│   │   │   ├── 󰌛 ResourceAttributes.cs
│   │   │   └── 󰌛 TraceAttributes.cs
│   │   ├──  Services
│   │   │   ├── 󰌛 OtlpLogsService.cs
│   │   │   ├── 󰌛 OtlpMetricsService.cs
│   │   │   └── 󰌛 OtlpTraceService.cs
│   │   ├──  Stores
│   │   │   ├──  Models
│   │   │   ├──  Serialization
│   │   │   ├── 󰌛 InMemoryTelemetryStore.cs
│   │   │   ├── 󰌛 ServiceRegistry.cs
│   │   │   ├── 󰌛 SqliteTelemetryStore.cs
│   │   │   └── 󰌛 TraceAggregator.cs
│   │   ├──  Streaming
│   │   │   ├── 󰌛 StreamingContracts.cs
│   │   │   └── 󰌛 TelemetryBroadcaster.cs
│   │   ├──  Telemetry
│   │   │   ├── 󰌛 GracefulShutdownService.cs
│   │   │   ├── 󰌛 HealthCheckExtensions.cs
│   │   │   ├── 󰌛 ObservabilityExtensions.cs
│   │   │   ├── 󰌛 OpenApiExtensions.cs
│   │   │   ├── 󰌛 ReceiverMetrics.cs
│   │   │   ├── 󰌛 ServerHardeningExtensions.cs
│   │   │   └── 󰌛 StatusEndpoint.cs
│   │   ├──  appsettings.json
│   │   ├──  CLAUDE.md
│   │   ├── 󰌛 dashboard.receiver.csproj
│   │   ├──  Dockerfile
│   │   ├── 󰌛 Program.cs
│   │   └── 󰌛 TelemetryConstants.cs
│   ├──  Dashboard.TestClient
│   │   ├── 󰌛 Dashboard.TestClient.csproj
│   │   └── 󰌛 Program.cs
│   ├──  Dashboard.TestClient.Go
│   │   ├──  go.mod
│   │   ├──  go.sum
│   │   ├──  main.go
│   │   └── 󰂺 README.md
│   ├──  Dashboard.TestClient.Node
│   │   ├──  index.js
│   │   ├──  package-lock.json
│   │   ├──  package.json
│   │   └── 󰂺 README.md
│   ├──  Dashboard.TestClient.Python
│   │   ├──  main.py
│   │   ├── 󰂺 README.md
│   │   └──  requirements.txt
│   ├──  Dashboard.Web
│   │   ├──  dist
│   │   │   ├──  assets
│   │   │   └──  index.html
│   │   ├──  public
│   │   ├── 󰣞 src
│   │   │   ├──  api
│   │   │   ├──  components
│   │   │   ├──  hooks
│   │   │   ├──  stores
│   │   │   ├──  types
│   │   │   ├──  utils
│   │   │   ├──  App.tsx
│   │   │   ├──  index.css
│   │   │   ├──  main.tsx
│   │   │   └──  vite-env.d.ts
│   │   ├──  CLAUDE.md
│   │   ├──  components.json
│   │   ├──  Dockerfile
│   │   ├──  index.html
│   │   ├── 󱁻 nginx.conf
│   │   ├──  package-lock.json
│   │   ├──  package.json
│   │   ├──  tsconfig.json
│   │   ├──  tsconfig.tsbuildinfo
│   │   └──  vite.config.ts
│   ├──  lol
│   │   ├──  docs
│   │   │   ├──  ARCHITECTURE.md
│   │   │   ├──  SEMCONV_MIGRATION_README.md
│   │   │   └──  WEAVER_INTEGRATION.md
│   │   ├──  jq
│   │   │   └──  semconv_schema_diff.jq
│   │   ├──  lol.Analyzers
│   │   │   ├── 󰌛 DeprecatedAttributeAnalyzer.cs
│   │   │   ├── 󰌛 DeprecatedAttributeCodeFixProvider.cs
│   │   │   ├── 󰌛 DeprecatedAttributes.cs
│   │   │   ├── 󰌛 lol.Analyzers.csproj
│   │   │   └── 󰌛 MissingSchemaUrlAnalyzer.cs
│   │   ├──  lol.Analyzers.Tests
│   │   │   ├──  Verifiers
│   │   │   ├── 󰌛 DeprecatedAttributeAnalyzerTests.cs
│   │   │   ├── 󰌛 DeprecatedAttributeCodeFixTests.cs
│   │   │   ├── 󰌛 lol.Analyzers.Tests.csproj
│   │   │   └── 󰌛 MissingSchemaUrlAnalyzerTests.cs
│   │   ├──  lol.Cli
│   │   │   ├── 󰌛 lol.Cli.csproj
│   │   │   └── 󰌛 Program.cs
│   │   ├──  lol.Collector
│   │   │   ├── 󰌛 lol.Collector.csproj
│   │   │   └── 󰌛 OttlGenerator.cs
│   │   ├──  lol.Schema
│   │   │   ├── 󰌛 lol.Schema.csproj
│   │   │   └── 󰌛 SchemaCore.cs
│   │   ├──  lol.Tests
│   │   │   ├── 󰌛 lol.Tests.csproj
│   │   │   ├── 󰌛 OttlGeneratorTests.cs
│   │   │   ├── 󰌛 SchemaDifferTests.cs
│   │   │   ├── 󰌛 SchemaParserTests.cs
│   │   │   ├── 󰌛 SchemaVersionTests.cs
│   │   │   ├── 󰌛 TestBuilders.cs
│   │   │   ├── 󰌛 TestConstants.cs
│   │   │   ├── 󰌛 TestFixtures.cs
│   │   │   └── 󰌛 TestTheoryData.cs
│   │   ├──  templates
│   │   │   └──  registry
│   │   ├──  CLAUDE.md
│   │   ├── 󰂺 README.md
│   │   └──  TODO.md
│   ├──  ZeroCode
│   │   ├──  compose.yaml
│   │   ├──  otel-collector-config.yaml
│   │   └── 󰂺 README.md
│   ├──  ZeroCode.DotNet
│   │   ├──  build.sh
│   │   ├──  Dockerfile
│   │   ├── 󰌛 Program.cs
│   │   ├── 󰂺 README.md
│   │   ├── 󰌛 SampleApp.csproj
│   │   └──  VERIFICATION_REPORT.md
│   ├──  ZeroCode.GitTracker
│   │   ├──  Dockerfile
│   │   ├── 󰌛 Program.cs
│   │   └── 󰌛 ZeroCode.GitTracker.csproj
│   ├──  ZeroCode.Go
│   │   ├──  Dockerfile
│   │   ├──  go.mod
│   │   ├──  main.go
│   │   └── 󰂺 README.md
│   ├──  ZeroCode.Java
│   │   ├── 󰣞 src
│   │   │   └──  main
│   │   ├──  Dockerfile
│   │   ├── 󰡯 mvnw
│   │   ├──  mvnw.cmd
│   │   └──  pom.xml
│   ├──  ZeroCode.Node
│   │   ├──  app.js
│   │   ├──  Dockerfile
│   │   ├──  package-lock.json
│   │   └──  package.json
│   ├──  ZeroCode.PHP
│   │   ├──  composer.json
│   │   ├──  Dockerfile
│   │   ├──  index.php
│   │   ├──  otel-bootstrap.php
│   │   ├── 󰂺 README.md
│   │   └──  VERIFICATION_REPORT.md
│   ├──  ZeroCode.Python
│   │   ├──  app.py
│   │   ├──  Dockerfile
│   │   ├── 󰂺 README.md
│   │   ├──  requirements.txt
│   │   └──  VERIFICATION_REPORT.md
│   ├──  CLAUDE.md
│   ├──  compose.yaml
│   └──  ZERO_CODE_PM_PROMPT.md
├──  build.cmd
├──  build.ps1
├──  build.sh
├──  CLAUDE.md
├──  compose.yaml
├──  Dockerfile.runtime
├──  Paperless.Telemetry.slnx
└── 󰂺 README.md
ancplua@Alexanders-MacBook-Air Paperless.Telemetry %
