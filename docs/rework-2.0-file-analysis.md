# Rework 2.0: File-by-File Analysis

## Zusammenfassung

| Kategorie | Zeilen | Aktion |
|-----------|--------|--------|
| `domains/` (OTel Semconv) | 13,398 | **LÖSCHEN** |
| `otel/` (OTel Span/Metric) | 1,862 | **80% LÖSCHEN** |
| `api/` (REST Routes) | 1,193 | **BEHALTEN** |
| `common/` (Primitives) | 814 | **50% BEHALTEN** |
| `node_modules/` | 4,415 | (ignorieren) |
| **TOTAL** | **21,840** | → **~2,000** |

**Reduktion: ~90%** (21,840 → ~2,000 Zeilen)

---

## `domains/` - 100% OTel Semconv = LÖSCHEN

Diese Dateien sind 1:1 Kopien von OTel Semantic Conventions und bringen keinen Mehrwert:

### AI Domain (1,144 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `ai/genai.tsp` | 1,144 | GenAI Semconv v1.39 | **DELETE** - Use OTel upstream |

### Data Domain (810 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `data/db.tsp` | 810 | Database Semconv | **DELETE** |
| `data/file.tsp` | ~150 | File Semconv | **DELETE** |
| `data/elasticsearch.tsp` | ~200 | ES Semconv | **DELETE** |
| `data/vcs.tsp` | ~150 | VCS Semconv | **DELETE** |
| `data/artifact.tsp` | ~100 | Artifact Semconv | **DELETE** |

### Transport Domain (~1,500 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `transport/http.tsp` | 600 | HTTP Semconv | **DELETE** |
| `transport/rpc.tsp` | 309 | RPC Semconv | **DELETE** |
| `transport/messaging.tsp` | 304 | Messaging Semconv | **DELETE** |
| `transport/url.tsp` | ~150 | URL Semconv | **DELETE** |
| `transport/signalr.tsp` | ~100 | SignalR (MS-specific) | **DELETE** |
| `transport/kestrel.tsp` | ~100 | Kestrel (MS-specific) | **DELETE** |
| `transport/user-agent.tsp` | ~50 | User-Agent Semconv | **DELETE** |

### Infra Domain (~1,500 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `infra/k8s.tsp` | 358 | Kubernetes Semconv | **DELETE** |
| `infra/container.tsp` | ~200 | Container Semconv | **DELETE** |
| `infra/cloud.tsp` | ~250 | Cloud Semconv | **DELETE** |
| `infra/faas.tsp` | ~200 | FaaS Semconv | **DELETE** |
| `infra/host.tsp` | ~150 | Host Semconv | **DELETE** |
| `infra/os.tsp` | ~100 | OS Semconv | **DELETE** |
| `infra/webengine.tsp` | ~250 | WebEngine Semconv | **DELETE** |

### Runtime Domain (~900 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `runtime/system.tsp` | 400 | System Semconv | **DELETE** |
| `runtime/dotnet.tsp` | 287 | .NET Semconv | **DELETE** |
| `runtime/process.tsp` | ~150 | Process Semconv | **DELETE** |
| `runtime/thread.tsp` | ~50 | Thread Semconv | **DELETE** |
| `runtime/aspnetcore.tsp` | ~100 | ASP.NET Core Semconv | **DELETE** |

### Security Domain (~1,200 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `security/security-rule.tsp` | 372 | Security Rule Semconv | **DELETE** |
| `security/network.tsp` | 315 | Network Semconv | **DELETE** |
| `security/tls.tsp` | 284 | TLS Semconv | **DELETE** |
| `security/dns.tsp` | ~150 | DNS Semconv | **DELETE** |

### Observe Domain (~2,500 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `observe/log.tsp` | 472 | Log Semconv | **DELETE** |
| `observe/otel.tsp` | 455 | OTel Meta Semconv | **DELETE** |
| `observe/error.tsp` | 420 | Error Semconv | **DELETE** |
| `observe/test.tsp` | 348 | Test Semconv | **DELETE** |
| `observe/feature-flags.tsp` | 339 | Feature Flag Semconv | **DELETE** |
| `observe/browser.tsp` | 336 | Browser Semconv | **DELETE** |
| `observe/session.tsp` | 314 | Session Semconv | **KEEP PARTIAL** - qyl session aggregation |
| `observe/exceptions.tsp` | ~100 | Exception Semconv | **DELETE** |

### Ops Domain (~650 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `ops/deployment.tsp` | 349 | Deployment Semconv | **DELETE** |
| `ops/cicd.tsp` | 284 | CI/CD Semconv | **DELETE** |

### Identity Domain (~300 Zeilen)
| File | Lines | Content | Action |
|------|-------|---------|--------|
| `identity/user.tsp` | ~150 | User Semconv | **DELETE** |
| `identity/geo.tsp` | ~150 | Geo Semconv | **DELETE** |

---

## `otel/` - Teilweise OTel, teilweise qyl-spezifisch

| File | Lines | Content | Action |
|------|-------|---------|--------|
| `otel/span.tsp` | ~400 | OTel Span Model | **DELETE** - use OTel proto |
| `otel/resource.tsp` | ~200 | OTel Resource | **DELETE** - use OTel proto |
| `otel/logs.tsp` | ~200 | OTel Log | **DELETE** - use OTel proto |
| `otel/metrics.tsp` | ~200 | OTel Metrics | **DELETE** - use OTel proto |
| `otel/enums.tsp` | ~150 | SpanKind, StatusCode | **DELETE** - use OTel proto |
| `otel/storage.tsp` | ~700 | **SpanRecord, SessionSummary** | **KEEP** - qyl DuckDB schema |

---

## `api/` - 100% qyl-spezifisch = BEHALTEN

| File | Lines | Content | Action |
|------|-------|---------|--------|
| `api/routes.tsp` | ~900 | REST API Endpoints | **KEEP** |
| `api/streaming.tsp` | ~300 | SSE Definitions | **KEEP** |

---

## `common/` - Gemischt

| File | Lines | Content | Action |
|------|-------|---------|--------|
| `common/types.tsp` | ~500 | Scalar types (SpanId, TraceId) | **KEEP** - qyl primitives |
| `common/errors.tsp` | ~150 | Error types | **KEEP** |
| `common/pagination.tsp` | ~150 | Pagination | **KEEP** |

---

## Nach dem Rework: Neue Struktur

```
core/specs/
├── main.tsp                    # Entry point
├── package.json
├── tspconfig.yaml
│
├── api/                        # qyl REST API (KEEP)
│   ├── routes.tsp              # All endpoints
│   └── streaming.tsp           # SSE
│
├── storage/                    # qyl DuckDB (KEEP)
│   └── schema.tsp              # SpanRecord, SessionSummary
│
├── common/                     # qyl Primitives (KEEP)
│   ├── scalars.tsp             # SpanId, TraceId, etc.
│   ├── errors.tsp
│   └── pagination.tsp
│
└── extensions/                 # qyl Extensions (NEW)
    └── qyl.tsp                 # cost_usd, session aggregation
```

**Total: ~2,000 Zeilen** (statt 21,840)

---

## Woher kommen OTel Types dann?

### Option 1: Generiert aus OTel YAML

```bash
# vendor/otel-semconv/model/*.yaml → src/qyl.protocol/OTel/*.g.cs
weaver generate \
  --templates templates/csharp \
  --output src/qyl.protocol/OTel/ \
  vendor/otel-semconv/model/
```

### Option 2: OTel Proto direkt

```protobuf
// Verwende direkt die OTel Proto Definitionen
import "opentelemetry/proto/trace/v1/trace.proto";
import "opentelemetry/proto/common/v1/common.proto";
```

### Option 3: NuGet Package

```xml
<PackageReference Include="OpenTelemetry.SemanticConventions" Version="1.0.0" />
```

---

## Konkrete Lösch-Befehle

```bash
# Phase 1: domains/ komplett löschen
rm -rf core/specs/domains/

# Phase 2: otel/ aufräumen (nur storage.tsp behalten)
rm core/specs/otel/span.tsp
rm core/specs/otel/resource.tsp
rm core/specs/otel/logs.tsp
rm core/specs/otel/metrics.tsp
rm core/specs/otel/enums.tsp
# KEEP: core/specs/otel/storage.tsp → mv to storage/schema.tsp

# Phase 3: Reorganisieren
mkdir -p core/specs/storage
mv core/specs/otel/storage.tsp core/specs/storage/schema.tsp
rm -rf core/specs/otel/

mkdir -p core/specs/extensions
# Create new qyl.tsp for extensions
```
