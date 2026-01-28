# qyl Rework 2.0: OTel Upstream Integration

## Vision

**Vorher:** qyl kopiert OTel Semantic Conventions manuell in TypeSpec
**Nachher:** qyl konsumiert OTel Semconv direkt aus Upstream und definiert nur qyl-Spezifisches

## Prinzip

```
┌─────────────────────────────────────────────────────────────┐
│                    OTel Semantic Conventions                │
│            github.com/open-telemetry/semantic-conventions   │
│                                                             │
│  model/gen-ai.yaml    model/db.yaml    model/http.yaml     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      OTel Weaver Tool                       │
│              Generiert Code aus YAML Definitionen           │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│  C# Constants    │ │  TypeScript      │ │  JSON Schema     │
│  *.g.cs          │ │  types.g.ts      │ │  *.schema.json   │
└──────────────────┘ └──────────────────┘ └──────────────────┘
              │               │               │
              └───────────────┼───────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                      qyl TypeSpec                           │
│                   NUR qyl-Spezifisches:                     │
│                                                             │
│  • API Routes (/api/v1/sessions, /api/v1/traces)           │
│  • Storage Models (DuckDB Schema, Indexes)                  │
│  • Promoted Columns (welche OTel-Attrs → DuckDB Cols)      │
│  • qyl Extensions (cost_usd, session aggregation)          │
└─────────────────────────────────────────────────────────────┘
```

## Was wird GELÖSCHT

### TypeSpec Files zum Entfernen/Radikal Kürzen

| Datei | Zeilen | Aktion |
|-------|--------|--------|
| `domains/ai/genai.tsp` | ~1145 | **90% löschen** - nur qyl-Extensions behalten |
| `domains/data/db.tsp` | ~200 | **80% löschen** - OTel DB Semconv entfernen |
| `domains/transport/http.tsp` | ~300 | **Komplett löschen** - 100% OTel |
| `domains/transport/rpc.tsp` | ~150 | **Komplett löschen** - 100% OTel |
| `domains/infra/container.tsp` | ~200 | **Komplett löschen** - 100% OTel |
| `domains/infra/k8s.tsp` | ~400 | **Komplett löschen** - 100% OTel |
| `domains/infra/cloud.tsp` | ~250 | **Komplett löschen** - 100% OTel |
| `domains/security/*.tsp` | ~500 | **Komplett löschen** - 100% OTel |
| ... | ... | ... |

**Geschätzte Reduktion: ~5000+ Zeilen → ~500 Zeilen**

## Was BLEIBT in TypeSpec

### 1. API Routes (`api/routes.tsp`)

```typespec
// BLEIBT - das ist qyl-spezifisch
@route("/api/v1")
namespace Qyl.Api;

interface SessionsApi {
  @get @route("/sessions") list(): SessionListResponse;
  @get @route("/sessions/{id}") get(@path id: string): SessionResponse;
}

interface TracesApi {
  @get @route("/traces/{traceId}") get(@path traceId: string): TraceResponse;
}

interface StatsApi {
  @get @route("/stats/tokens") tokens(): TokenStatsResponse;
  @get @route("/stats/latency") latency(): LatencyStatsResponse;
}

interface HealthApi {
  @get @route("/health") ready(): HealthResponse;
  @get @route("/alive") live(): { @statusCode statusCode: 200 | 503 };
}
```

### 2. Storage Models (`storage/storage.tsp`)

```typespec
// BLEIBT - DuckDB Schema ist qyl-spezifisch
@doc("DuckDB storage row for spans")
@extension("x-duckdb-table", "spans")
model SpanRecord {
  // Primary Key
  @extension("x-duckdb-primary-key", true)
  span_id: string;

  // Foreign Keys & Indexes
  @extension("x-duckdb-index", "idx_trace_id")
  trace_id: string;

  @extension("x-duckdb-index", "idx_session_id")
  session_id?: string;

  // Timestamps (OTel format)
  start_time_unix_nano: int64;
  end_time_unix_nano: int64;

  // PROMOTED COLUMNS - qyl-spezifische Entscheidung welche OTel-Attrs schnell sein sollen
  @extension("x-promoted-from", "gen_ai.provider.name")
  gen_ai_provider_name?: string;

  @extension("x-promoted-from", "gen_ai.request.model")
  gen_ai_request_model?: string;

  @extension("x-promoted-from", "gen_ai.usage.input_tokens")
  gen_ai_input_tokens?: int64;

  @extension("x-promoted-from", "gen_ai.usage.output_tokens")
  gen_ai_output_tokens?: int64;

  // QYL EXTENSION - nicht in OTel Semconv
  @extension("x-qyl-extension", true)
  gen_ai_cost_usd?: float64;

  // Alles andere → JSON blob
  attributes_json: string;
  resource_json: string;
}
```

### 3. qyl Extensions (`extensions/qyl.tsp`)

```typespec
// BLEIBT - qyl-spezifische Erweiterungen die NICHT in OTel sind
namespace Qyl.Extensions;

@doc("qyl-specific: Estimated cost in USD")
model CostEstimate {
  input_cost_usd: float64;
  output_cost_usd: float64;
  total_cost_usd: float64;
  pricing_model?: string;
}

@doc("qyl-specific: Session aggregation")
model SessionSummary {
  session_id: string;
  span_count: int32;
  error_count: int32;
  total_input_tokens: int64;
  total_output_tokens: int64;
  total_cost_usd?: float64;
  first_span_time: int64;
  last_span_time: int64;
}
```

## Neue Dependency: OTel Semantic Conventions

### ✅ Gewählt: NPM Package → TypeScript → C# Generator

```json
{
  "devDependencies": {
    "@opentelemetry/semantic-conventions": "^1.39.0"
  }
}
```

**Warum NPM?**
1. Offiziell gepflegt von OTel
2. Klare TypeScript `.d.ts` Definitionen
3. Einfach zu parsen (kein YAML/Jinja2)
4. Semver: Minor updates sind safe für stable, incubating nur für breaking

**Generator Pipeline:**
```
@opentelemetry/semantic-conventions (NPM)
           ↓
   generate-csharp.ts (parst .d.ts)
           ↓
   GenAiSemanticConventions.g.cs
```

**Befehle:**
```bash
cd core/specs
npm run generate:otel  # → src/qyl.protocol/OTel/GenAiSemanticConventions.g.cs
```

**Alternativen verworfen:**
- Git Submodule + Weaver: Zu komplex, braucht custom Jinja2 templates
- NuGet: Package ist seit 2022 unlisted/deprecated
- Python generator: Funktioniert, aber TS-basiert ist konsistenter mit unserem Stack

## Build Pipeline Änderungen

### Vorher

```
TypeSpec → OpenAPI → C# Models → DuckDB Schema
                  → TypeScript Types
```

### Nachher

```
┌─────────────────────────────────────────────────────────┐
│                    Build Pipeline                        │
└─────────────────────────────────────────────────────────┘

1. OTel Semconv (vendor/otel-semconv/model/*.yaml)
   │
   ├──► weaver generate → SemanticConventions.g.cs
   │                    → semconv.g.ts
   │
   └──► Verfügbar für Validation

2. TypeSpec (core/specs/*.tsp) - NUR qyl-spezifisch
   │
   └──► tsp compile → openapi.yaml (nur API routes)
                    → storage-schema.json

3. Code Generation
   │
   ├──► openapi.yaml → api.g.ts (TypeScript client)
   ├──► storage-schema.json → DuckDbSchema.g.cs
   └──► Combine with SemanticConventions.g.cs
```

## NUKE Targets

```csharp
// Neue Targets
Target SyncOtelSemconv => _ => _
    .Executes(() =>
    {
        // Git submodule update
        Git("submodule update --init --recursive");
    });

Target GenerateOtelConstants => _ => _
    .DependsOn(SyncOtelSemconv)
    .Executes(() =>
    {
        // weaver generate für C# und TypeScript
        // Oder custom Generator der YAML liest
    });

Target TypeSpecCompile => _ => _
    .DependsOn(GenerateOtelConstants)
    .Executes(() =>
    {
        // Nur noch qyl-spezifische TypeSpec
    });
```

## Migration Guide

### Phase 1: Setup (Week 1)

- [ ] Git Submodule für OTel Semconv hinzufügen
- [ ] Weaver oder custom Generator aufsetzen
- [ ] Erste `SemanticConventions.g.cs` generieren

### Phase 2: TypeSpec Cleanup (Week 2-3)

- [ ] `domains/` Ordner analysieren - was ist 100% OTel?
- [ ] OTel-only Files löschen
- [ ] Remaining Files auf qyl-Extensions reduzieren
- [ ] Tests anpassen

### Phase 3: Integration (Week 4)

- [ ] Build Pipeline aktualisieren
- [ ] CI/CD anpassen
- [ ] Documentation updaten

### Phase 4: Validation (Week 5)

- [ ] Alle Tests grün
- [ ] OpenAPI Spec validieren
- [ ] Dashboard funktioniert
- [ ] OTLP Ingestion funktioniert

## Risiken

| Risiko | Mitigation |
|--------|-----------|
| OTel Semconv breaking changes | Pin auf spezifische Version (v1.39.0) |
| Weaver Tool instabil | Fallback: Custom YAML Parser |
| TypeSpec kann OTel nicht referenzieren | Generierte Types importieren |
| Dashboard Types brechen | Parallel generieren, dann switchen |

## Erfolgsmetriken

- [ ] TypeSpec Zeilen: 5000+ → <500
- [ ] Keine manuell gepflegten OTel Attribute
- [ ] Build Zeit: gleichbleibend oder besser
- [ ] Alle existierenden Tests grün
- [ ] OTel Semconv Version klar dokumentiert

## Offene Fragen

1. **Weaver vs Custom Generator?** - Weaver ist offiziell, aber komplex. Custom ist einfacher aber mehr Maintenance.

2. **TypeScript Generation?** - OTel hat kein offizielles TS Package für Semconv. Müssen wir selbst generieren.

3. **Backwards Compatibility?** - Sollen alte API Responses weiter funktionieren? Oder clean break?

4. **ANcpLua.NET.Sdk Sync?** - Soll das SDK auch von OTel upstream generieren?
