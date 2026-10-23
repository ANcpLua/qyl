# observe\_runtime — PRD

Oct 23, 2026 · @Alexander Nachtmann

## 1. Zweck

`observe_runtime` ist ein MCP-Tool (Spec 2026-07-28), das einem Agenten – auch einem kleinen Modell wie Haiku – typisierte Funde aus einem selbstinstrumentierten Prozess liefert statt roher Telemetrie. Die Instrumentierung entsteht zur Compile-Zeit aus den gepinnten stabilen OpenTelemetry Semantic Conventions; das Binary ist NativeAOT, ingestiert sich selbst und braucht weder CLR-Profiler noch Sampling noch Live-Attach.

Erfolg ist messbar an drei Zahlen: G2 grün (Registry == Inventar), R2 bei 0 Allokationen ohne Listener, T3 unter 64 KiB Ausgabe bei 500.000 Spans.

## 2. Problem

- Verbose Tool-Antworten in der Größenordnung von 500.000 Zeilen sprengen den Kontext jedes Agenten; kleine Modelle fallen zuerst aus.
- Live-Attach (Inspector, Profiler, eBPF) ist sprachspezifisch, öffnet Debug-Ports und braucht Consent pro Prozess und Zeitfenster.
- Semconv-Vokabular wird in Instrumentierungen von Hand abgeschrieben; Drift zwischen Registry und Code fällt erst im Backend auf, nie im Build.

## 3. Lösung und Architektur

1. Ein Weaver-getriebener `IIncrementalGenerator` liest das gepinnte Stable-Inventar (21 Areas, 121 Attribute, 8 Span-Formen, 51 Instrumente, 1 Event, 5 Resources; Commit `3e25804…`, 2026-10-23) und erzeugt Marker-Attribute sowie die `TelemetryModel`-Union.
2. Marker an Call-Sites werden zu Interceptoren (`InterceptsLocation`), die Activity, Metrik oder Log-Event mit den stabilen Attributen und `code.*`-Konstanten emittieren; NativeAOT kompiliert sie zu nativem Code.
3. Export per OTLP in den Collector/Store des Binaries. Trace-Kontext kommt über MCP-`_meta` (`traceparent`) herein und geht an Sub-Tools hinaus.
4. `observe_runtime(target, scope, window)` holt per MRTR den Consent (signierter `requestState`), aktiviert den Listener idempotent, fragt den Store ab und liefert `Findings[]` plus Handle; Drill-down nur über `evidence_ref`.
5. Der Vertrag liegt in TypeSpec (diskriminierte Unions, `kind`); Emitter für C# und TypeScript, JSON über Source-Generator.

## 4. Scope und Nicht-Ziele

**Im Scope:** eigene .NET-11-Binaries (C# 15, NativeAOT); Stable-only-Semconv; MCP 2026-07-28 (stateless, Handles, MRTR; Tasks-Extension optional); die Signale Traces, Metrics, Logs/Events und Resource.

**Nicht-Ziele:** fremde Prozesse (dafür OBI/eBPF, ein separates Produkt); Development-Konventionen wie `gen-ai`, `mcp.*`, `process`, `rpc`; Profiles und Baggage (keine stabilen Ziele im Inventar); Sampling; jeder Schreibzugriff auf das beobachtete System.

## 5. Präzision, Beobachtungsfenster, Verifikationsgrenze

Präzision ist eine Build-Eigenschaft: Was beobachtbar ist, legt die Menge der markierten Call-Sites fest. Ein Zeitfenster begrenzt nur, welche Records eine Query sieht. Es gibt keine Attach-Dauer und keinen Consent pro Zeitfenster, nur ein Handle mit konfigurierbarer TTL.

Die Verifikationsgrenze trennt zwei Beweisarten: Anforderungen G werden zur Compile-Zeit bewiesen (Roslyn, JIT-Assembly), R, S und T nur durch exportierte OTLP-Records (Runtime-Assembly, JIT und AOT). Diese Trennung bestimmt den Harness in Abschnitt E2E.

## G: Generator-Invarianten (Compile-Time)

Diese Invarianten werden ohne Laufzeit bewiesen: über `CSharpGeneratorDriver` gegen eine Test-Compilation. Sie bilden die Compile-Zeit-Seite der Verifikationsgrenze.

| ID | Invariante | Beweis |
| --- | --- | --- |
| G1 | Jeder Marker löst zu **genau einer** `TelemetryModel`-Case auf (Span, Metric, Event, Resource). Ungültige Kombinationen erzeugen eine Diagnostic, nie Code. | Diagnostic-ID vorhanden, kein `InterceptsLocation` emittiert |
| G2 | Das generierte Registry ist auf Commit `3e25804…` / `9ffc7e0…` gepinnt. Attribut-Keys = exakt 121; Modelle = 8 Spans, 51 Instrumente, 1 Event, 5 Resources. | Golden-Snapshot der generierten Registry gegen `stable-semconv-matrix.json` |
| G3 | Kein deprecated oder nicht-stabiles Attribut ist über einen Marker emittierbar (`exception.escaped`, `db.query.parameter`, `cpu.mode`, `url.template`, `jvm.gc.cause`, `tls.protocol.version`). | Marker mit diesem Key → Diagnostic, Compilation bleibt sonst grün |
| G4 | Enum-Attribute akzeptieren zur Compile-Zeit nur stabile Listenwerte. `db.system.name` ∈ {mariadb, microsoft.sql\_server, mysql, postgresql}; `http.request.method` ohne `QUERY`. | `redis` → Diagnostic; `QUERY` → `_OTHER` + `http.request.method_original` oder Diagnostic |
| G5 | Interceptoren werden nur für markierte Call-Sites emittiert; Anzahl `InterceptsLocation` = Anzahl Marker-Treffer. Nicht markierte Sites bleiben unberührt. | Syntax-Walker über den generierten Baum |
| G6 | Der Generator ist deterministisch und inkrementell: zweimal dieselbe Eingabe → byte-identische Ausgabe; unveränderte Eingabe → alle Steps `Cached`/`Unchanged`. | `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)`, Step-Reasons prüfen |
| G7 | Generierter Code ist AOT-sauber: keine Reflection, kein `dynamic`, kein `MakeGenericType`. | `IsAotCompatible=true` + `TreatWarningsAsErrors`: 0 × IL2xxx/IL3xxx |
| G8 | Der Marker trägt nur Konstanten, Enums oder Typen; die Union-Auflösung passiert im Generator, nie im Attribut-Konstruktor. | Attribut-Signatur per Reflection über die Marker-Assembly (Testseite darf reflektieren) |
| G9 | Required-Level aus Semconv wird in generierten Code übernommen: ein Interceptor für `span.http.server` ohne `http.request.method`, `url.path` und `url.scheme` kompiliert nicht. | Diagnostic bei fehlender Pflicht-Dimension |
| G10 | Interceptoren nutzen ausschließlich `GetInterceptableLocation()` (Checksummen-Form, C# 13+); die obsolete `(Pfad, Zeile, Spalte)`-Form kommt nicht vor. Der Namespace der Interceptoren steht im `<InterceptorsNamespaces>` des Konsumenten, sonst CS9137. | Generierter Baum ohne 3-Argument-`InterceptsLocation`; Test-Compilation mit und ohne Property |
| G11 | Call-Site-Herkunft ist Compile-Zeit-Konstante: jeder Interceptor stampft `code.function.name`, `code.line.number` und `code.file.path` (repo-relativ via `PathMap`, nie absolut). | Konstanten im generierten Baum; kein Laufwerksbuchstabe oder `/home` im Pfad |

G2 ist der Anker: Er macht die Semconv-Version zur Compile-Zeit-Konstante. Ändert sich der Pin, bricht genau ein Snapshot-Test, nicht fünfzig Laufzeittests.

## R: Interceptor-Laufzeit

Diese Invarianten brauchen einen echten Export: In-Memory-Exporter oder OTLP-Receiver im Testprozess. Sie bilden die Laufzeit-Seite der Verifikationsgrenze.

| ID | Invariante | Assertion |
| --- | --- | --- |
| R1 | Semantik des Originals bleibt: Rückgabewert identisch, Exception-Instanz identisch (kein Wrapping, kein `AggregateException`), `CancellationToken` wird durchgereicht, `Activity.Current` ist nach dem Call wieder der vorherige Wert. | `Assert.AreSame(thrown, caught)`; `Assert.AreSame(before, Activity.Current)` |
| R2 | Ohne Listener ist der Wrapper nach einem Warm-up-Call allokationsfrei und ruft das Original direkt. | `GC.GetAllocatedBytesForCurrentThread()` Delta = 0 ab dem zweiten Call bei `HasListeners() == false` |
| R3 | Span-Form stimmt: `span.http.server` → `ActivityKind.Server`, Name `{method} {route}` (ohne Route nur `{method}`); `span.db.client` → `ActivityKind.Client`, Name `{db.query.summary}` wenn vorhanden, sonst `{db.operation.name} {db.collection.name}`. | Kind, DisplayName, Pflichtattribute aus G9 |
| R4 | `error.type` nur bei Fehler, Status `Error`; Erfolg → kein `error.type`, Status `Unset` (nie `Ok`). HTTP-Server: 5xx und Exceptions sind Fehler, 4xx **nicht**; HTTP-Client: 4xx und 5xx sind Fehler. | Attribut-Absenz prüfen; `[DataRow(404, server, false)]`, `[DataRow(404, client, true)]`, `[DataRow(503, server, true)]` |
| R5 | Exceptions landen als **Log-Event** `exception` mit `exception.type`, `exception.message`, `exception.stacktrace` und Trace/Span-Korrelation. `exception.escaped` wird nie emittiert. Kein Span-Event (deprecated). | LogRecord mit `EventName == "exception"`, `Activity.Events` ohne `exception` |
| R6 | Metrik-Instrumente entsprechen dem Inventar: `http.server.request.duration` ist ein Histogram mit Unit `s` (nicht `ms`), Counter/UpDownCounter/Gauge je nach Instrument. Gebundene Attribute ⊆ stabile Dimensionen. | Instrument-Typ, Unit, Dimensionsmenge |
| R7 | Resource trägt die vier Pflicht-Keys `service.name`, `telemetry.sdk.name`, `telemetry.sdk.version`, `telemetry.sdk.language == dotnet`; `service.version`, `service.instance.id`, `service.namespace` sind vorhanden, wenn konfiguriert. | 4 Keys immer; die 3 optionalen gegen die Konfiguration |
| R8 | Trace-Kontext aus `_meta.traceparent` wird extrahiert: Server-Activity ist Kind der Client-Activity, auch über stdio. Fehlendes `_meta` → neuer Root, keine Exception. Ausgehende `callTool` (b → bb) injizieren `traceparent`. | `ParentSpanId == client.SpanId`; Root ohne Parent |
| R9 | Keine Doppel-Emission: Wenn ASP.NET Core, `HttpClient` oder EF Core ein Instrument bereits melden, emittiert der Interceptor es nicht zusätzlich. | Pro Meter-/Instrument-Name genau eine Quelle |
| R10 | Async-Interceptoren beenden die Activity erst nach Abschluss des `Task`/`ValueTask`, nicht beim Return des Wrappers. | `Activity.Duration` ≥ simulierte Verzögerung |
| R11 | Jeder Interceptor-Span trägt `code.function.name`, `code.line.number` und repo-relatives `code.file.path` als Konstanten, die der abgefangenen Call-Site entsprechen. Das ist der wörtliche Beweis für „precise observability“. | Werte == Call-Site aus dem Testquelltext; Pfad ohne Laufwerk/Home |
| R12 | `StartActivity` kann `null` liefern (kein Listener, Sampler); der Wrapper ruft das Original dann ohne jede Telemetrie-Operation auf. | Kein `NullReferenceException`, Rückgabewert identisch |

R4 und R5 sind die, die bei .NET-Teams am häufigsten falsch sind: `Activity.AddException` erzeugt ein Span-Event, das ist die deprecated Konvention; stabil ist nur der Log-Pfad über `ILogger` mit Exception.

## S: Modell und Serialisierung

TypeSpec besitzt den Vertrag, der C#-Emitter übersetzt ihn, `System.Text.Json` Source-Gen serialisiert ihn. Jede Invariante prüft, dass auf dem Weg nichts verloren geht.

| ID | Invariante | Assertion |
| --- | --- | --- |
| S1 | `TelemetryModel` ist exhaustiv: ein `switch` ohne `default` über alle Cases kompiliert; ein zusätzlicher Case bricht den Build (Compile-Time-Test über `CSharpCompilation`). | Diagnostic für fehlenden Case vorhanden |
| S2 | Diskriminator `kind` trägt exakt den TypeSpec-Variantennamen (`span`, `metric`, `event`, `resource`), Envelope `none`. Die C#-15-Default-Union-Serialisierung ergänzt ihn nicht automatisch; der Emitter muss ihn setzen. | JSON enthält `"kind":"metric"` auf Top-Level |
| S3 | Round-Trip ist verlustfrei und idempotent: `Serialize(Deserialize(json)) == json` byte-identisch bei kanonischer Property-Reihenfolge. | `Assert.AreEqual(json, roundTripped)` |
| S4 | Der `JsonSerializerContext` deckt jede Union-Case und jeden Outcome-Typ ab; kein Fallback auf Reflection. | `JsonSerializerOptions.TypeInfoResolver` liefert für jeden Typ ohne Exception |
| S5 | Outcome-Union \`QuerySuccess | InvalidQuery |
| S6 | Hot-Path-Unions sind nicht geboxt: Die Compiler-Union speichert `object?` und boxt Werttypen; für Interceptor-Pfade wird eine eigene Repräsentation benutzt. | Allokations-Test wie R2 auf dem Union-Pfad |
| S7 | Eine leere Union (`default`) wird beim Serialisieren abgelehnt, nicht als `null` geschrieben. | `Assert.ThrowsExactly<InvalidOperationException>` |
| S8 | JSONL-Streaming über `SerializeAsyncEnumerable` mit generiertem `JsonTypeInfo<T>` liefert eine Zeile pro Record; `kind` steht in jeder Zeile. | Zeilenzahl = Recordzahl, jede Zeile parsebar |

S2 ist der Punkt, an dem TypeSpec und C# 15 auseinanderlaufen können. Der Emitter-Test ist deshalb ein Golden-Test gegen die TypeSpec-Ausgabe, nicht gegen die C#-Defaults.

## T: MCP-Toolvertrag observe\_runtime

Der Vertrag ist gegen das Spec 2026-07-28 formuliert: stateless, Handles als Tool-Argumente, MRTR für Nachfragen, `resultType` auf jedem Result.

| ID | Invariante | Assertion |
| --- | --- | --- |
| T1 | Idempotenz des Handles: gleiche `(target, scope, window)` → gleiche `observation_id`; abweichendes Fenster → anderes Handle. | Zwei Calls, `Assert.AreEqual(id1, id2)` |
| T2 | Funde sind dedupliziert über `fingerprint`, nie über Span-IDs; derselbe Fund in 10.000 Spans erscheint einmal mit `occurrences = 10000`. | `Assert.ContainsSingle` nach Fingerprint |
| T3 | Ausgabe validiert gegen `outputSchema`. `structuredContent` ist maßgeblich; der `content`-Textblock enthält dieselbe JSON-Serialisierung (Spec: SHOULD, für ältere Clients). Größe ist beschränkt unabhängig vom Ingest-Volumen; 64 KiB ist Startwert, kein Naturgesetz. | 500.000 Spans rein → `structuredContent` < 64 KiB; `content[0].text` == serialisiertes `structuredContent` |
| T4 | Kein Roh-Record verlässt das Tool: keine `span_id`, kein Stacktrace, kein `db.query.text` in der Ausgabe; Drill-down nur über `evidence_ref`-Handle. | Negativ-Suche über die serialisierte Ausgabe |
| T5 | `resultType` ist immer gesetzt. Erster Call ohne Consent → `input_required` mit `inputRequests` und `requestState`; Retry mit `inputResponses` + unverändertem `requestState` → `complete`; Retry ohne `inputResponses` → erneut `input_required`, kein Fehler. | Drei Calls, drei `resultType`-Werte |
| T6 | Der Server ist stateless: Consent lebt **signiert in `requestState`**, nicht im Serverspeicher. Ein Retry mit manipuliertem oder fremdem `requestState` liefert `input_required`, nie `complete`. Serverneustart zwischen Call und Retry ändert das Ergebnis nicht. | Byte in `requestState` kippen → `input_required`; Neustart-Test |
| T7 | Aktivierung ist ein No-op, wenn schon aktiv: zweimal `observe_runtime` für dasselbe Ziel → ein Listener, nicht zwei. | `ActivityListener`-Zahl = 1 |
| T8 | Fenstersemantik: Nur Records mit `[start, end)` fließen in Funde; Randwerte sind definiert, nicht zufällig. | Record exakt bei `end` ausgeschlossen |
| T9 | Stable-only-Filter: Funde referenzieren nur die 121 stabilen Keys. Nicht-stabile Keys erscheinen unter `extensions`, nie als Pflichtfeld. | Key-Menge ⊆ Inventar |
| T10 | Profiles und Baggage liefern `DataUnavailable`, nicht leeren `QuerySuccess`: null stabile Ziele im Inventar. | `kind == "dataUnavailable"` mit Grund |
| T11 | Result-`_meta` trägt `io.modelcontextprotocol/serverInfo`; der Tool-Span ist Kind des `traceparent` aus dem Request. | Meta-Keys und Parent-Beziehung |
| T12 | `tools/list` ist deterministisch sortiert und trägt `ttlMs` und `cacheScope` (Spec: MUST). | Zwei Listen byte-identisch |
| T13 | Domänenfehler (`InvalidQuery`) kommen als Union-Case mit `isError: false`; Protokollfehler als JSON-RPC-Error. Nie beides für denselben Zustand. | Fehlerklassifikation pro Testfall |
| T14 | Tool-Annotations sagen die Wahrheit: `idempotentHint: true` (T1, T7), `readOnlyHint: false` (Listener wird aktiviert), `destructiveHint: false`, `openWorldHint: false`. | Annotations aus `tools/list` gegen das Verhalten in T1/T7 |

T3 und T4 sind die Produkt-Invarianten: Sie beweisen, dass Haiku als Agent nie die 500.000 Zeilen sieht. Alles andere ist Protokoll-Hygiene.

## E2E-Harness (MSTest 4.x, Microsoft.Testing.Platform)

Der Harness exportiert in-process nach OTLP und asserted gegen das Exportierte, nie gegen `Activity`-Objekte allein. So wird die Verifikationsgrenze real: Was der Test sieht, ist das, was ein Collector sieht.

**Lifecycle**

1. `[AssemblyInitialize]`: `TracerProvider`, `MeterProvider`, `LoggerProvider` mit `InMemoryExporter<T>` pro Signal; eine `ActivitySource` pro generiertem Interceptor-Namespace.
2. `[ClassInitialize]`: Semconv-Inventar aus `stable-semconv-matrix.json` laden (Commit-Pin wird im Test verglichen, nicht angenommen).
3. `[TestInitialize]`: Exporter leeren, deterministische `TimeProvider`, `Activity.Current = null`.
4. `[TestCleanup]`: `ForceFlush` mit `context.CancellationToken`; nicht gelesene Records gelten als Fehler (verhindert stille Doppel-Emission, R9).
5. `[AssemblyCleanup]`: Provider `Dispose`, Golden-Snapshots bei Abweichung als `context.AddResultFile` anhängen.

**Regeln für den Harness**

- **Zwei Test-Assemblies, nicht eine.** `*.Generator.Tests` referenziert Roslyn (`Microsoft.CodeAnalysis.CSharp`) und läuft nur unter JIT; `*.Runtime.Tests` enthält R/S/T und läuft unter JIT **und** `PublishAot`. Roslyn ist nicht AOT-fähig, also können G-Tests nie im AOT-Lauf leben.
- `[DependsOn]` gilt nur innerhalb einer Assembly. Wurzel der Runtime-Assembly ist deshalb `Inventory_EmbeddedRegistry_MatchesStableMatrix`: vergleicht das vom Generator eingebettete Registry (Konstanten) mit `stable-semconv-matrix.json`, ohne Roslyn. Die Generator-Assembly hat ihre eigene Wurzel `Registry_PinnedCommit_MatchesStableMatrixSnapshot`. Die Reihenfolge der Assemblies erzwingt CI, nicht ein Attribut.
- Testklassen `sealed`, `TestContext` über den Primär-Konstruktor, Token überall durchreichen (`MSTEST0049`).
- Attribut-Prüfungen mit `Assert.Scope()` (experimentell, `#pragma warning disable MSTESTEXP`): alle fehlenden Pflichtattribute einer Span-Form in einem Lauf sehen, nicht eines pro Durchlauf.
- `[CombinatorialData]` über die 8 Span-Formen und die 51 Instrumentnamen; die Werte kommen aus dem geladenen Inventar, nicht aus Literalen im Test.
- `[ResourceLock(WellKnownResources.EnvironmentVariables)]` für Tests, die `OTEL_*`-Variablen setzen; alles andere bleibt parallel.
- Golden-Dateien unter `TestContext.TestTempDirectory`, Normalisierung von Trace-/Span-IDs und Timestamps vor dem Vergleich; der Vergleich ist `Assert.AreEquivalent(expected, actual, strict: true)` auf dem deserialisierten Modell, nicht String-Diff.
- AOT-Lauf der Runtime-Assembly mit `EnableMSTestSourceGeneration`; Host-Injection ist dort nicht erlaubt (`MSTEST0088`), also Services statisch halten. **Offen:** ob `[CombinatorialData]` und `[DependsOn]` unter Source-Generation vollständig unterstützt sind, zuerst mit einem Zwei-Methoden-Projekt prüfen, bevor der Katalog darauf gebaut wird.
- `[OSCondition(OperatingSystems.Linux)]` nur auf Tests, die einen echten OTLP-Receiver per Socket brauchen; der Rest läuft in-memory überall.

**Was der Harness bewusst nicht tut:** Er sampelt nicht und filtert nicht. Jeder Record wird exportiert, jeder Record wird gelesen. Die Beschränkung der Ausgabe (T3) ist eine Eigenschaft des Tools, nicht des Harness.

## Testmethoden-Katalog

Namensschema: `Subjekt_Bedingung_ErwartetesErgebnis`. Eine Klasse pro Invariantenfamilie. Jede Zeile ist eine Methode; die Assertion-Spalte ist das, was sie beweist.

**`SemconvGeneratorTests` (G)**

| Methode | Inv. | Assertion |
| --- | --- | --- |
| `Marker_WithSpanAndMetricTarget_ReportsAmbiguousModelDiagnostic` | G1 | Diagnostic-ID `OTGEN001`, 0 generierte Interceptoren |
| `Registry_PinnedCommit_MatchesStableMatrixSnapshot` | G2 | `Assert.AreEquivalent(matrix, generated, strict: true)`; Zähler 121/8/51/1/5 |
| `Marker_DeprecatedAttributeKey_ReportsDiagnostic` | G3 | `[DataRow]` über `exception.escaped`, `db.query.parameter`, `cpu.mode`, `url.template`, `jvm.gc.cause`, `tls.protocol.version` |
| `Marker_NonStableEnumValue_ReportsDiagnostic` | G4 | `db.system.name = redis` → Diagnostic; `postgresql` → keine |
| `Marker_HttpMethodQuery_FallsBackToOtherWithOriginal` | G4 | Generierter Code setzt `_OTHER` und `http.request.method_original` |
| `Generator_MarkedCallSites_EmitsOneInterceptorEach` | G5 | `InterceptsLocation`-Anzahl == Marker-Anzahl |
| `Generator_UnmarkedCallSite_LeavesSyntaxUntouched` | G5 | Keine Diagnostic, kein Interceptor für die Site |
| `Generator_SameInput_ProducesByteIdenticalOutput` | G6 | Zwei Läufe, `Assert.AreSequenceEqual(bytes1, bytes2)` |
| `Generator_UnchangedInput_AllStepsCached` | G6 | Alle `IncrementalStepRunReason` ∈ {Cached, Unchanged} |
| `GeneratedCode_AotAnalyzers_ReportNoTrimOrAotWarnings` | G7 | 0 Diagnostics mit Präfix IL2/IL3 |
| `MarkerAttribute_ConstructorArguments_AreConstantCompatible` | G8 | Kein Parameter vom Union-Typ |
| `Interceptor_HttpServerSpan_MissingRequiredAttribute_FailsCompilation` | G9 | Diagnostic `OTGEN009` ohne `http.request.method` |
| `Generator_UsesInterceptableLocationOnly_NoLegacyPathForm` | G10 | Kein `InterceptsLocation` mit 3 Argumenten im generierten Baum |
| `Consumer_WithoutInterceptorsNamespaces_FailsWithCS9137` | G10 | Diagnostic CS9137 ohne Property, grün mit Property |
| `Interceptor_CodeAttributes_AreCompileTimeConstantsOfCallSite` | G11 | `code.function.name`/`code.line.number` als Literale; `code.file.path` ohne Laufwerk oder `/home` |

**`InterceptorRuntimeTests` (R)**

| Methode | Inv. | Assertion |
| --- | --- | --- |
| `Intercepted_ThrowingCall_PropagatesSameExceptionInstance` | R1 | `Assert.AreSame(expected, Assert.ThrowsExactly<...>(...))`; nie `AggregateException` |
| `Intercepted_Call_RestoresActivityCurrent` | R1 | `Assert.AreSame(before, Activity.Current)` |
| `Intercepted_CancellationToken_ReachesOriginal` | R1 | Original sieht denselben Token |
| `Intercepted_NoListener_AllocatesNothingAfterWarmup` | R2 | Erster Call verworfen, Allokations-Delta des zweiten == 0 |
| `HttpServerSpan_Exported_HasServerKindAndMethodRouteName` | R3 | `Kind == Server`, Name `GET /orders/{id}` |
| `DbClientSpan_WithQuerySummary_UsesSummaryAsName` | R3 | Name == `db.query.summary` |
| `DbClientSpan_WithoutQuerySummary_UsesOperationAndCollection` | R3 | Name `SELECT orders` |
| `SpanShape_AllEight_RequiredAttributesPresent` | R3 | `[CombinatorialData]` über 8 Formen, `Assert.Scope()` |
| `FailedCall_SetsErrorTypeAndErrorStatus` | R4 | `error.type` gesetzt, Status Error |
| `SucceededCall_HasNoErrorTypeAndUnsetStatus` | R4 | Attribut fehlt, Status Unset |
| `HttpStatus_ByKindAndCode_SetsErrorOnlyPerSemconv` | R4 | `[DataRow(404, Server, false)]`, `[DataRow(404, Client, true)]`, `[DataRow(503, Server, true)]` |
| `Exception_ExportedAsLogEventNotSpanEvent` | R5 | LogRecord `EventName == "exception"`, 3 Attribute, `Activity.Events` leer |
| `Exception_NeverEmitsEscapedAttribute` | R5 | Kein Key `exception.escaped` in irgendeinem Signal |
| `MetricInstrument_AllFiftyOne_KindAndUnitMatchInventory` | R6 | `[CombinatorialData]` über Instrumentnamen |
| `HttpServerDuration_IsHistogramInSeconds` | R6 | Unit `s`, Wert < 10 für 120-ms-Request |
| `Resource_Exported_CarriesRequiredServiceAndSdkKeys` | R7 | `service.name` + 3 × `telemetry.sdk.*`, `language == dotnet` |
| `Resource_Exported_CarriesOptionalKeysWhenConfigured` | R7 | `service.version`, `service.instance.id`, `service.namespace` gegen Konfiguration |
| `IncomingMeta_WithTraceparent_ParentsServerSpanToClient` | R8 | `ParentSpanId == clientSpanId` über stdio |
| `IncomingMeta_Missing_StartsNewRootWithoutThrowing` | R8 | `ParentId == null` |
| `OutgoingCallTool_InjectsTraceparentIntoMeta` | R8 | `_meta.traceparent` matcht W3C-Regex |
| `HttpServerDuration_AspNetCoreAndInterceptor_EmitOnce` | R9 | Genau ein Instrument dieses Namens im Export |
| `AsyncInterceptor_EndsActivityAfterAwait` | R10 | `Duration ≥ 50 ms` bei 50-ms-Delay |
| `Span_CodeAttributes_MatchInterceptedCallSite` | R11 | Funktionsname und Zeile == Call-Site im Testquelltext |
| `Span_CodeFilePath_IsRepoRelative` | R11 | Pfad beginnt nicht mit `/`, `\\` oder Laufwerk |
| `Intercepted_StartActivityReturnsNull_CallsOriginalWithoutTelemetry` | R12 | Kein Throw, Rückgabe identisch, 0 Spans |

**`TelemetryModelSerializationTests` (S)**

| Methode | Inv. | Assertion |
| --- | --- | --- |
| `TelemetryModel_SwitchWithoutDefault_IsExhaustive` | S1 | Compilation ohne Case-Diagnostic |
| `TelemetryModel_AddedCase_BreaksExhaustiveSwitch` | S1 | Diagnostic nach Einfügen einer fünften Case |
| `Serialize_EachCase_EmitsKindFromTypeSpecVariantName` | S2 | `[DataRow]` span/metric/event/resource |
| `RoundTrip_EachCase_IsByteIdentical` | S3 | `Assert.AreEqual(json, reserialized)` |
| `SerializerContext_CoversAllCasesAndOutcomes` | S4 | `GetTypeInfo` für jeden Typ non-null |
| `Outcome_InvalidQuery_SerializesAsCaseNotException` | S5 | `kind == "invalidQuery"`, kein Throw |
| `HotPathUnion_ValueCase_AllocatesNothing` | S6 | Allokations-Delta == 0 |
| `Serialize_DefaultUnion_Throws` | S7 | `Assert.ThrowsExactly<InvalidOperationException>` |
| `StreamJsonl_TenRecords_TenLinesEachWithKind` | S8 | 10 Zeilen, jede mit `kind` |

**`ObserveRuntimeContractTests` (T)**

| Methode | Inv. | Assertion |
| --- | --- | --- |
| `Observe_SameTargetScopeWindow_ReturnsSameHandle` | T1 | `Assert.AreEqual(id1, id2)` |
| `Observe_DifferentWindow_ReturnsDifferentHandle` | T1 | `Assert.AreNotEqual` |
| `Findings_TenThousandIdenticalSpans_OneFindingWithOccurrences` | T2 | `Assert.ContainsSingle(f => f.Fingerprint == fp, findings)`, `Occurrences == 10000` |
| `Output_FiveHundredThousandSpans_StaysUnderSizeBound` | T3 | `Assert.IsInRange(0, 65536, bytes)` |
| `Output_ValidatesAgainstOutputSchema` | T3 | Schema-Validator ohne Fehler |
| `Output_ContentTextBlock_EqualsSerializedStructuredContent` | T3 | `content[0].text == JSON(structuredContent)` |
| `Output_ContainsNoRawIdentifiersOrQueryText` | T4 | Negativ-Suche über `span_id`, `db.query.text`, Stacktrace |
| `Observe_WithoutConsent_ReturnsInputRequiredWithRequestState` | T5 | `resultType == "input_required"`, `requestState` nicht leer |
| `Observe_RetryWithInputResponses_ReturnsComplete` | T5 | `resultType == "complete"` |
| `Observe_RetryWithoutInputResponses_ReturnsInputRequiredAgain` | T5 | Kein Error, erneut `input_required` |
| `Observe_RetryWithTamperedRequestState_NeverCompletes` | T6 | Ein Byte gekippt → `input_required` |
| `Observe_RetryAfterServerRestart_StillCompletes` | T6 | Neuer Serverprozess, gleicher `requestState` → `complete` |
| `Observe_CalledTwice_RegistersSingleListener` | T7 | Listener-Zahl == 1 |
| `Window_RecordAtEndBoundary_IsExcluded` | T8 | Record bei `end` nicht in Funden |
| `Findings_Keys_AreSubsetOfStableInventory` | T9 | `Assert.ContainsAll(inventoryKeys, findingKeys)` |
| `Query_ProfilesSignal_ReturnsDataUnavailable` | T10 | `kind == "dataUnavailable"` |
| `Query_BaggageSignal_ReturnsDataUnavailable` | T10 | dito |
| `Result_Meta_CarriesServerInfoAndParentsToolSpan` | T11 | Meta-Key vorhanden, Parent-Beziehung |
| `ToolsList_TwoCalls_ByteIdenticalWithTtlAndScope` | T12 | `Assert.AreSequenceEqual`, `ttlMs > 0` |
| `DomainError_IsUnionCaseWithIsErrorFalse` | T13 | `isError == false` |
| `ProtocolError_IsJsonRpcErrorNotUnionCase` | T13 | JSON-RPC-Error-Code gesetzt |
| `ToolAnnotations_MatchObservedBehavior` | T14 | `idempotentHint == true`, `readOnlyHint == false`, `destructiveHint == false`, `openWorldHint == false` |

Wurzeln: In der Generator-Assembly ist `Registry_PinnedCommit_MatchesStableMatrixSnapshot` die Wurzel; in der Runtime-Assembly `InventoryTests.Inventory_EmbeddedRegistry_MatchesStableMatrix` (eingebettete Generator-Konstanten gegen die Matrix, ohne Roslyn). `[DependsOn]` zeigt innerhalb jeder Assembly auf ihre Wurzel. Scheitert der Pin, fällt alles kontrolliert aus.

## Risiken und Rust-Fallback

Drei Risiken sind real, der Rest ist Fleißarbeit.

| Risiko | Wo es auffällt | Gegenmaßnahme |
| --- | --- | --- |
| Doppel-Emission: ASP.NET Core, HttpClient und EF Core emittieren `http.*` und `db.*` bereits selbst. Ein Interceptor an derselben Stelle verdoppelt Spans und Histogramme. | R9, `[TestCleanup]`-Restprüfung | Interceptoren nur an Call-Sites ohne eingebaute Instrumentierung; für Framework-Pfade den eingebauten Meter abonnieren, nicht ersetzen |
| Exception-Pfad: `Activity.AddException` ist die deprecated Span-Event-Konvention; der stabile Pfad ist `ILogger` mit Exception. .NET-Teams greifen reflexhaft zum ersten. | R5 | Generator emittiert nie `AddException`; Analyzer-Diagnostic, wenn Anwendungscode es in markiertem Scope tut |
| C#-15-Union-Boxing und fehlender Diskriminator: Compiler-Unions speichern `object?` und schreiben `kind` nicht von selbst. | S2, S6 | Eigene Union-Repräsentation im Hot-Path; Emitter setzt `kind` explizit; Golden-Test gegen TypeSpec-Ausgabe |
| .NET 11 ist bis 9. November RC2: `AddTracing`, `ActivitySourceFactory`, generierte Logging-Methoden und `SerializeAsyncEnumerable` mit `JsonTypeInfo<T>` können sich bis GA noch bewegen. | G7, S8 | API-Nutzung hinter einer dünnen Adapterschicht; Tests laufen gegen RC2 und GA im selben CI |
| Interceptor-Bindung: `InterceptableLocation` kodiert eine Inhalts-Checksumme der Quelldatei plus Position. Jeder Build-Schritt, der Quelltext zwischen Generator und Compiler verändert (Formatter, Line-Ending-Normalisierung im Build), bricht die Bindung still; die Call-Site läuft dann uninstrumentiert. | G5, G10, R11 | Nur `GetInterceptableLocation()`; kein Pre-Compile-Formatter; G5 zählt Interceptoren gegen Marker **und** R11 prüft, dass jede markierte Site tatsächlich einen Span liefert |
| `code.file.path` leakt Build-Pfade (`C:\\Users\\…`, `/home/…`) in jeden Span, wenn `PathMap`/`Deterministic` fehlt. | R11 | `<PathMap>$(MSBuildProjectDirectory)=/src</PathMap>`, `Deterministic=true`; Test prüft Pfadform |
| Signierter `requestState` braucht einen Schlüssel. Rotation macht offene Consents ungültig; ein verlorener Schlüssel macht alle ungültig. | T6 | Schlüssel-ID im `requestState`, zwei aktive Schlüssel während Rotation; `input_required` statt Fehler bei unbekannter ID |

**Fallback-Kriterium Rust.** Nur dann wechseln, wenn G7 oder S6 nicht haltbar sind, also wenn AOT-Sauberkeit oder Allokationsfreiheit im Hot-Path mit C# 15 nicht erreichbar ist. Alles andere (Spec-Vertrag, Semconv-Pin, Tool-Invarianten) ist sprachneutral und wandert unverändert mit. Ein Wechsel wegen Unions, TypeSpec oder MCP-Protokoll wäre kein Grund: Beide Seiten haben dieselben Bausteine, C# hat mehr vorhandenen Code.

**Was nach dieser Spezifikation noch fehlt:** die erste grüne Ausführung von `Registry_PinnedCommit_MatchesStableMatrixSnapshot`. Das ist der einzige Test, der nichts voraussetzt. Alles andere hängt davon ab.

### Änderungsprotokoll

**2026-10-23, 23:50 — Review:** sieben Korrekturen, drei Ergänzungen; Intro von Gesprächsform auf PRD umgestellt.

**Korrigiert**

1. `[DependsOn]` gilt nur innerhalb einer Assembly, und Roslyn-Tests können nicht unter AOT laufen → zwei Assemblies mit je eigener Wurzel (Harness, Katalog-Schluss).
2. Resource: nur `service.name` und `telemetry.sdk.*` sind Pflicht; „8 Keys“ war erfunden (R7).
3. HTTP-Fehlerregel fehlte: 4xx ist serverseitig kein Fehler, clientseitig schon (R4).
4. DB-Spanname: `db.query.summary` hat Vorrang vor `{operation} {collection}` (R3).
5. Interceptor-Bindung ist checksummenbasiert, nicht pfadbasiert; die alte 3-Argument-Form ist obsolet (G10, Risiken).
6. `structuredContent` ersetzt `content` nicht; der Textblock trägt dieselbe JSON (T3).
7. Allokationstest braucht Warm-up (R2).

**Ergänzt**

- Consent muss im **signierten `requestState`** leben, weil der Server stateless ist; Neustart- und Manipulationstest (T6). Das ist die wichtigste Ergänzung: Ohne sie ist der Consent-Flow nur im Testprozess korrekt.
- `code.function.name` / `code.line.number` / `code.file.path` als Compile-Zeit-Konstanten pro Interceptor (G11, R11): der wörtliche Beweis für „precise observability“, kostenlos, weil der Generator die Call-Site kennt.
- Tool-Annotations müssen zum Verhalten passen, insbesondere `idempotentHint: true` (T14); `StartActivity == null`-Pfad (R12); `<InterceptorsNamespaces>` (G10).

**Offene Punkte:** ob `[CombinatorialData]` und `[DependsOn]` unter `EnableMSTestSourceGeneration` laufen; mit einem Zwei-Methoden-Projekt prüfen, bevor der Katalog darauf baut. Erster Test, der grün werden muss: `Inventory_EmbeddedRegistry_MatchesStableMatrix`.

### Quellen

- [MCP 2026-07-28 Key Changes](https://modelcontextprotocol.io/specification/2026-07-28/changelog)
- [MCP 2026-07-28 Deprecated Features](https://modelcontextprotocol.io/specification/2026-07-28/deprecated)
- [OTel Semantic Conventions for MCP](https://opentelemetry.io/docs/specs/semconv/gen-ai/mcp)
- [semantic-conventions @ 3e25804](https://github.com/open-telemetry/semantic-conventions/commit/3e2580475b1fa266fe85a1d1ea7fe9c41a4783b5) (Stable-Inventar, Handoff vom 23.10.2026)
- [C# unions (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/union)
- [System.Text.Json union types](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/union-types)
- [TypeSpec discriminated types](https://typespec.io/docs/standard-library/discriminated-types/)
- [What's new in .NET 11 libraries](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries)
- MSTest 4.5.1 API-Referenz (modern-mstest Skill)
