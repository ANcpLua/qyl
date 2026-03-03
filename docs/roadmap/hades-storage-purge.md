# HADES: Storage Layer Purge — qyl.instrumentation.generators

**Typ:** Reines Lösch- und Ersetzungsmandat
**Scope:** `qyl.collector/Storage/` + `qyl.instrumentation.generators/`
**Ziel:** Generator-Projekt löschen, handgeschriebene INSERT-Pipeline durch DuckDB Appender ersetzen, kein altes Muster übrig lassen.

---

## Was gelöscht wird (keine Ausnahmen)

| Was | Pfad | Grund |
|-----|------|-------|
| Gesamtes Projekt | `src/qyl.instrumentation.generators/` | Wird durch Appender ersetzt |
| Generator-Referenz | `src/qyl.collector/qyl.collector.csproj` → `<ProjectReference ... instrumentation.generators ...>` | Entfällt |
| `[DuckDbTable]` Attribut auf `SpanStorageRow` | `DuckDbReaderExtensions.cs:259` | Entfällt |
| `[DuckDbColumn(IsUBigInt = true)]` Attribute | `DuckDbReaderExtensions.cs:271-273` | Entfällt |
| `[DuckDbColumn(ExcludeFromInsert = true)]` | `DuckDbReaderExtensions.cs:303` | Entfällt |
| `partial` Keyword auf `SpanStorageRow` | `DuckDbReaderExtensions.cs:260` | Kein Generator mehr |
| `SpanColumnCount = 26` Konstante | `DuckDbStore.cs:31` | Appender kennt Spaltenanzahl selbst |
| `LogColumnCount = 16` Konstante | `DuckDbStore.cs:32` | Appender kennt Spaltenanzahl selbst |
| `SpanColumnList` Konstante (24 Zeilen) | `DuckDbStore.cs:34-58` | Appender braucht keine Spaltenliste |
| `LogColumnList` Konstante | `DuckDbStore.cs:60-...` | Appender braucht keine Spaltenliste |
| `SpanOnConflictClause` Konstante | `DuckDbStore.cs:45` | Appender hat keinen ON CONFLICT Support — separate Upsert-Logik wenn nötig |
| Manueller INSERT-Aufbau (Spans) | `DuckDbStore.cs:1419-1450` | Ersetzt durch `AppendRecords()` |
| Manueller INSERT-Aufbau (Logs) | `DuckDbStore.cs:1454-1480` | Ersetzt durch `AppendRecords()` |
| `SpanStorageRow.MapFromReader()` Aufruf | `DuckDbStore.cs``:1672` | Bleibt für SELECT-Queries — NUR der INSERT-Pfad ändert sich |
| `ulong` → `decimal` Cast für UBIGINT | `DuckDbEmitter.cs` + generator intern | Appender handled `ulong` nativ (UBIGINT support bestätigt) |
| Projekt-Eintrag in `qyl.slnx` | Zeile mit `qyl.instrumentation.generators.csproj` | Entfällt |

---

## Was geschrieben wird (Ersatz)

### 1. `SpanStorageRowMap` — Appender-Mapping

**Neue Datei:** `src/qyl.collector/Storage/SpanAppender.cs`

```csharp
// DuckDB Mapped Appender für SpanStorageRow
// Ersetzt: DuckDbInsertGenerator + AddParameters + BuildMultiRowInsertSql
// DuckDB.NET v1.4.4 — ulong → UBIGINT nativ unterstützt

using DuckDB.NET.Data;
using DuckDB.NET.Data.Mapping;

namespace qyl.collector.Storage;

public sealed class SpanStorageRowMap : DuckDBAppenderMap<SpanStorageRow>
{
    public SpanStorageRowMap()
    {
        Map(r => r.SpanId);
        Map(r => r.TraceId);
        Map(r => r.ParentSpanId);
        Map(r => r.SessionId);
        Map(r => r.Name);
        Map(r => r.Kind);
        Map(r => r.StartTimeUnixNano);   // ulong → UBIGINT, kein decimal-Cast
        Map(r => r.EndTimeUnixNano);     // ulong → UBIGINT, kein decimal-Cast
        Map(r => r.DurationNs);          // ulong → UBIGINT, kein decimal-Cast
        Map(r => r.StatusCode);
        Map(r => r.StatusMessage);
        Map(r => r.ServiceName);
        Map(r => r.GenAiProviderName);
        Map(r => r.GenAiRequestModel);
        Map(r => r.GenAiResponseModel);
        Map(r => r.GenAiInputTokens);
        Map(r => r.GenAiOutputTokens);
        Map(r => r.GenAiTemperature);
        Map(r => r.GenAiStopReason);
        Map(r => r.GenAiToolName);
        Map(r => r.GenAiToolCallId);
        Map(r => r.GenAiCostUsd);
        Map(r => r.AttributesJson);
        Map(r => r.ResourceJson);
        Map(r => r.BaggageJson);
        Map(r => r.SchemaUrl);
        DefaultValue();  // CreatedAt — von DuckDB DEFAULT CURRENT_TIMESTAMP gesetzt
    }
}
```

### 2. `LogStorageRowMap` — Appender-Mapping

```csharp
public sealed class LogStorageRowMap : DuckDBAppenderMap<LogStorageRow>
{
    public LogStorageRowMap()
    {
        Map(r => r.LogId);
        Map(r => r.TraceId);
        Map(r => r.SpanId);
        Map(r => r.SessionId);
        Map(r => r.TimeUnixNano);         // ulong → UBIGINT
        Map(r => r.ObservedTimeUnixNano); // ulong? → nullable UBIGINT
        Map(r => r.SeverityNumber);
        Map(r => r.SeverityText);
        Map(r => r.Body);
        Map(r => r.ServiceName);
        Map(r => r.AttributesJson);
        Map(r => r.ResourceJson);
        Map(r => r.SourceFile);
        Map(r => r.SourceLine);
        Map(r => r.SourceColumn);
        Map(r => r.SourceMethod);
        DefaultValue();  // CreatedAt
    }
}
```

### 3. `DuckDbStore` — Bulk-Insert-Methoden ersetzen

**Vorher (zu löschen):**
```csharp
// ~60 Zeilen manueller INSERT-Aufbau mit $1..$N Parametern
var sb = new StringBuilder(1024);
sb.Append("INSERT INTO spans (").Append(SpanColumnList).Append(") VALUES ");
for (var i = 0; i < batch.Spans.Count; i++) { ... }
cmd.CommandText = sb.ToString();
// + AddParameters-Schleife
```

**Nachher:**
```csharp
using var appender = _connection.CreateAppender<SpanStorageRow, SpanStorageRowMap>("spans");
appender.AppendRecords(batch.Spans);
```

```csharp
using var appender = _connection.CreateAppender<LogStorageRow, LogStorageRowMap>("logs");
appender.AppendRecords(logRows);
```

---

## `SpanStorageRow` nach dem Purge

```csharp
// KEIN partial, KEINE Attribute, KEINE Generator-Marker
[DuckDbTable("spans")]  // ← WEG
public sealed partial record SpanStorageRow  // partial ← WEG
{
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    // ...
    [DuckDbColumn(IsUBigInt = true)]  // ← WEG
    public required ulong StartTimeUnixNano { get; init; }
    // ...
    [DuckDbColumn(ExcludeFromInsert = true)]  // ← WEG
    public DateTimeOffset? CreatedAt { get; init; }
}
```

Wird zu:
```csharp
public sealed record SpanStorageRow
{
    public required string SpanId { get; init; }
    public required string TraceId { get; init; }
    public string? ParentSpanId { get; init; }
    public string? SessionId { get; init; }
    public required string Name { get; init; }
    public required byte Kind { get; init; }
    public required ulong StartTimeUnixNano { get; init; }
    public required ulong EndTimeUnixNano { get; init; }
    public required ulong DurationNs { get; init; }
    public required byte StatusCode { get; init; }
    public string? StatusMessage { get; init; }
    public string? ServiceName { get; init; }
    public string? GenAiProviderName { get; init; }
    public string? GenAiRequestModel { get; init; }
    public string? GenAiResponseModel { get; init; }
    public long? GenAiInputTokens { get; init; }
    public long? GenAiOutputTokens { get; init; }
    public double? GenAiTemperature { get; init; }
    public string? GenAiStopReason { get; init; }
    public string? GenAiToolName { get; init; }
    public string? GenAiToolCallId { get; init; }
    public double? GenAiCostUsd { get; init; }
    public string? AttributesJson { get; init; }
    public string? ResourceJson { get; init; }
    public string? BaggageJson { get; init; }
    public string? SchemaUrl { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
}
```

Sauber. Kein Generator-Marker. Kein `partial`. Normales C#-Record.

---

## Wichtige Einschränkung: ON CONFLICT

Der aktuelle Code hat `ON CONFLICT (span_id) DO UPDATE SET ...` im INSERT-Statement. Der DuckDB Appender **unterstützt kein ON CONFLICT**.

**Zwei Optionen:**

### Option A — Pre-filter (empfohlen für qyl)
Vor dem Append duplikate rausfiltern. Da OTLP-Ingestion idempotent sein soll und Duplikate selten sind:

```csharp
// Nur neue spans, die noch nicht in DuckDB existieren
var existingIds = await GetExistingSpanIds(batch.Spans.Select(s => s.SpanId));
var newSpans = batch.Spans.Where(s => !existingIds.Contains(s.SpanId)).ToList();
if (newSpans.Count > 0)
{
    using var appender = connection.CreateAppender<SpanStorageRow, SpanStorageRowMap>("spans");
    appender.AppendRecords(newSpans);
}
```

### Option B — Fallback auf INSERT ON CONFLICT nur bei Konflikt
Append zuerst, bei Unique-Violation auf parameterized INSERT mit ON CONFLICT fallback. Selten ausgelöst.

**Hades entscheidet:** Option A. Pre-filter ist sauber, messbar, kein verstecktes Fehlerverhalten.

---

## Verbundene Aufräumarbeiten (im selben Commit)

| Was | Wo | Warum |
|-----|----|-------|
| `using Domain.CodeGen;` (bereits erledigt) | `BuildPipeline.cs` | War dead import |
| `Temperature` + `CostUsd` structs (bereits erledigt) | `Scalars.g.cs` | Ersetzt durch `double?` in Models |
| `SpanKind`/`SpanStatusCode` in `OTelEnums.cs` | `qyl.protocol/Enums/OTelEnums.cs` | Duplikat von `OTelEnumsEnums.g.cs` — nach Generator-Fix löschen |
| `using qyl.protocol.Enums;` (bereits erledigt) | `SpanRecord.cs` | Fehlte, jetzt korrekt |

---

## Verbote (Hades-Regel)

- **Kein** `[DuckDbTable]` / `[DuckDbColumn]` / `[DuckDbIgnore]` Attribut darf in irgendeiner `.cs` Datei bleiben
- **Kein** manueller `$1, $2, $3` INSERT-Parameter-Aufbau für spans oder logs
- **Kein** `(decimal)ulong` Cast in INSERT-Code
- **Kein** `partial record` ohne echten Generator dahinter
- **Keine** hardcoded Spaltenanzahl-Konstanten (`SpanColumnCount`, `LogColumnCount`)
- **Kein** Generator-Projekt in `qyl.slnx` oder `.csproj` Referenzen

---

## Verifikation nach dem Purge

```bash
# 1. Kein Generator mehr
ls src/qyl.instrumentation.generators/  # → Verzeichnis existiert nicht

# 2. Keine Generator-Marker
grep -r "DuckDbTable\|DuckDbColumn\|DuckDbIgnore" src/ --include="*.cs"  # → leer

# 3. Kein manueller INSERT-Aufbau
grep -r "SpanColumnList\|LogColumnList\|SpanColumnCount\|BuildMultiRowInsert" src/ --include="*.cs"  # → leer

# 4. Kein decimal-Cast für ulong
grep -r "(decimal).*ulong\|ulong.*decimal" src/qyl.collector/ --include="*.cs"  # → leer

# 5. Build sauber
nuke Compile

# 6. Funktionaler Smoke-Test
# → Span einliefern → DuckDB abfragen → Span ist da → kein Fehler
```

---

## Kontext: Warum das zur zero-cost-observability-proposal passt

Die `zero-cost-observability-proposal.md` beschreibt den Daten**fluss**: Interceptor erzeugt Span → Collector speichert Span. Der Generator-Ansatz war die Brücke zwischen beiden. Der Appender ist eine sauberere Brücke:

- **Kein Roslyn-Compiler-Plugin** für interne Collector-Infrastruktur
- **Kein parametrisierter SQL-String** auf dem heißen Ingestion-Pfad
- **Direkter DuckDB-Schreibzugriff** — genau das was bei tausenden Spans/s zählt
- **`SpanStorageRow` ist ein normales Record** — lesbar, testbar, keine Attributmagie

Der Generator löste ein reales Problem (25-Spalten-INSERT in Sync halten). Der Appender löst dasselbe Problem mit weniger Code, weniger Abhängigkeiten und mehr Durchsatz.
