# claude-code-workflows: codebase-cleanup — Skill-Audit

> Skill-Referenz und Duplikat-Analyse fuer `codebase-cleanup` (v1.2.0, wshobson/agents) und `reducing-entropy` (User Skill).
> Verglichen mit existierenden ancplua-Plugins: `code-simplifier`, `elegance-pipeline`, `exodia:hades`.

---

## Inhalt

| # | Skill | Zeilen | Phasen | Bewertung |
|---|-------|--------|--------|-----------|
| 1 | [`/codebase-cleanup:tech-debt`](#1-tech-debt) | 398 | 8 | Redundant |
| 2 | [`/codebase-cleanup:refactor-clean`](#2-refactor-clean) | 925 | 12 | Redundant |
| 3 | [`/codebase-cleanup:deps-audit`](#3-deps-audit) | 791 | 8 | Ersetzt durch code-simplifier:deps-audit (67 Zeilen) |
| 4 | [`/reducing-entropy`](#4-reducing-entropy) | 82 + 213 refs | 3 Fragen + 4 Mindsets | Einzigartig — behalten |

---

## 1. tech-debt

**Technical Debt Analysis and Remediation** — 398 Zeilen
Pfad: `~/.claude/plugins/cache/claude-code-workflows/codebase-cleanup/1.2.0/commands/tech-debt.md`

### Phasen

| # | Phase | Was es tut | Zeilen |
|---|-------|-----------|--------|
| 1 | Debt Inventory | 5 Kategorien: Code-Debt (Duplikate, Complexity >10, Methods >50 LOC, God Classes >500 LOC), Architecture-Debt (fehlende Abstraktionen, veraltete Frameworks, Deprecated APIs), Testing-Debt (Coverage-Luecken, flaky Tests), Documentation-Debt, Infrastructure-Debt | ~88 |
| 2 | Impact Assessment | ROI-Kalkulation pro Debt-Item: Stunden x $/h = Jahreskosten. Beispiel: "Duplicate validation in 5 files = 20h/Monat = $36,000/Jahr". Quality Impact: Bugs/Monat x Fix-Stunden. Risk: Critical/High/Medium/Low | ~30 |
| 3 | Debt Metrics Dashboard | YAML-KPIs: cyclomatic_complexity (current: 15.2, target: 10.0), code_duplication (23% -> 5%), test_coverage (unit 45%, integration 12%, e2e 5%). Trend-Analyse mit Python-Dict und Quartals-Projektion ("1200 by 2025_Q1 without intervention") | ~40 |
| 4 | Prioritized Remediation | Quick Wins (Woche 1-2, ROI 250-375%): Extract duplicate logic, add error monitoring, automate deploy. Medium-Term (Monat 1-3): God class refactoring, React 16->18 upgrade. Long-Term (Quartal 2-4): DDD, comprehensive test suite | ~55 |
| 5 | Implementation Strategy | Python-Beispiel: LegacyPaymentProcessor -> PaymentFacade -> PaymentService mit Feature-Flag. Team-Allokation: 20% Sprint-Capacity, tech_lead + senior_dev + dev | ~45 |
| 6 | Prevention Strategy | Pre-commit: complexity max 10, duplication max 5%, coverage min 80%. CI: no high vulns, no perf regression >10%. Code review: 2 approvals, must include tests. Debt Budget: max 2%/Monat Anstieg, 5%/Quartal Pflicht-Reduktion | ~35 |
| 7 | Communication Plan | Executive Summary Template: "Current debt score: 890 (High), Monthly velocity loss: 35%, Recommended investment: 500 hours, Expected ROI: 280% over 12 months". Developer Refactoring Guide | ~40 |
| 8 | Success Metrics | Monatlich: Debt -5%, Bugs -20%, Deploy-Freq +50%, Lead Time -30%, Coverage +10%. Quartalsweise: Architecture Health, Developer Satisfaction, Security Audit | ~20 |

### Bewertung

| Aspekt | Urteil |
|--------|--------|
| Nuetzlicher Kern | Phasen 1-4 (Inventory, Impact, Metrics, Roadmap) |
| Bloat | Phasen 5-7: generische Python-Templates (PaymentFacade, Feature-Flags), Stakeholder-Reports |
| Projektbewusstsein | Null — keine CLAUDE.md-Integration, keine Stack-Erkennung |
| Codebeispiele | Python (OrderProcessor, PaymentProcessor) — irrelevant fuer .NET/React |
| Ueberschneidung mit | `elegance-pipeline` (Scouts inventarisieren, Judges bewerten, Planner priorisiert), `exodia:hades` (Dead Code + Duplication Elimination), `exodia:mega-swarm` (6-12 Agents scannen parallel) |
| Fazit | **Redundant** — elegance-pipeline + exodia decken alle 8 Phasen ab, projektbewusst |

---

## 2. refactor-clean

**Refactor and Clean Code** — 925 Zeilen
Pfad: `~/.claude/plugins/cache/claude-code-workflows/codebase-cleanup/1.2.0/commands/refactor-clean.md`

### Phasen

| # | Phase | Was es tut | Zeilen |
|---|-------|-----------|--------|
| 1 | Code Analysis | Code Smells (Methods >20 LOC, Classes >200 LOC, Duplikate, Dead Code, Magic Numbers, Tight Coupling, Missing Abstractions), SOLID Violations, Performance Issues (O(n^2), Memory Leaks, Missing Caching) | ~40 |
| 2 | Refactoring Strategy | Immediate Fixes (Constants, Naming, Dead Code, Boolean Simplification), Method Extraction (Python Before/After), Class Decomposition, Pattern Application (Factory, Strategy, Observer, Repository, Decorator) | ~30 |
| 3 | SOLID Principles Tutorial | Vollstaendiges Lehrbuch mit Before/After: SRP (Python UserManager -> 4 Klassen), OCP (Python DiscountCalculator -> Strategy), LSP (TypeScript Rectangle/Square), ISP (Java Worker/Robot), DIP (Go MySQL -> Interface) | ~180 |
| 4 | Refactoring Scenarios | Scenario 1: Python OrderSystem 500 LOC -> Domain/Infra/Application Layer (Domain Entities, Repository Interface, MySQL Implementation, Validator, Service). Scenario 2: TypeScript Code Smell Catalog (Long Param List -> Interface, Feature Envy -> Move Method, Primitive Obsession -> Value Object) | ~100 |
| 5 | Decision Frameworks | Metrics Matrix (7 Metriken mit Good/Warning/Critical Schwellwerten), ROI-Formel: Priority = (Business Value x Technical Debt) / (Effort x Risk). Decision Tree: Production bugs? -> CRITICAL. Blocking features? -> HIGH. Frequently modified? -> MEDIUM | ~70 |
| 6 | Modern Practices 2024-2025 | GitHub Actions AI-Review (Copilot Autofix + CodeRabbit + Codium PR-Agent), Ruff pyproject.toml Config, mypy strict, ESLint + SonarJS + Security, Sourcery auto-refactoring YAML, SonarQube quality dashboard YAML, Semgrep security rules (SQL injection, hardcoded secrets), CodeQL | ~100 |
| 7 | Clean Code Principles | Meaningful names, single-purpose functions, no side effects, consistent abstraction levels, DRY, YAGNI | ~15 |
| 8 | Error Handling | Python custom exceptions (OrderValidationError, InsufficientInventoryError), fail-fast | ~15 |
| 9 | Testing Strategy | pytest Unit Tests, Coverage: all public methods, edge cases, error conditions, performance benchmarks | ~25 |
| 10 | Before/After Comparison | Template: "processData(): 150 lines, complexity: 25 -> validateInput(): 20 lines, complexity: 4" | ~20 |
| 11 | Migration Guide | 5 Steps + LegacyOrderProcessor Adapter Pattern (Python) | ~20 |
| 12 | Code Quality Checklist | 18 Checkboxen: Methods <20, Classes <200, Params <=3, Complexity <10, No nested loops >2, Coverage >80%, No hardcoded secrets, AI review passed, Static analysis clean | ~25 |

### Bewertung

| Aspekt | Urteil |
|--------|--------|
| Nuetzlicher Kern | Phase 1 (Code Analysis Checkliste) + Phase 5 (Decision Frameworks / ROI-Formel) |
| Massiver Bloat | Phase 3: 180 Zeilen SOLID-Tutorial in 5 Sprachen — ein LLM kennt SOLID bereits. Phase 4: 100 Zeilen Python-Refactoring-Beispiele. Phase 6: 100 Zeilen Tool-Configs (Ruff, ESLint, SonarQube) |
| Projektbewusstsein | Null — empfiehlt Ruff/ESLint/SonarQube statt Rider Inspections/Roslyn Analyzers |
| Codebeispiele | Python, TypeScript, Java, Go — kein C# |
| Ueberschneidung mit | `code-simplifier` Agent (macht genau das in 54 Zeilen, liest CLAUDE.md), `/simplify` User Skill |
| Fazit | **Redundant** — code-simplifier Agent ersetzt alle 12 Phasen, projektbewusst, 17x weniger Zeilen |

---

## 3. deps-audit

**Dependency Audit and Security Analysis** — 791 Zeilen
Pfad: `~/.claude/plugins/cache/claude-code-workflows/codebase-cleanup/1.2.0/commands/deps-audit.md`

### Phasen

| # | Phase | Was es tut | Zeilen |
|---|-------|-----------|--------|
| 1 | Dependency Discovery | Multi-Language: npm, pip, go, rust, dotnet, ruby, java, php. Python-Klasse `DependencyDiscovery` mit `_parse_npm_dependencies()`, `_parse_requirements_txt()`, etc. Baut vollstaendigen Tree inkl. transitiver Abhaengigkeiten mit Circular-Detection | ~140 |
| 2 | Vulnerability Scanning | Python-Klasse `VulnerabilityScanner` mit APIs: npm audit bulk, PyPI, Maven/OSS Index. Risk-Score-Berechnung: base_score x 1.5 (Exploit available) x 1.2 (publicly disclosed) x 2.0 (RCE). Severity-Analyse mit immediate_action_required Liste | ~120 |
| 3 | License Compliance | Python-Klasse `LicenseAnalyzer` mit Kompatibilitaetsmatrix (MIT/Apache/BSD/GPL/proprietary). Erkennt Copyleft-Risiken, erzeugt Markdown License-Report mit Tabelle | ~90 |
| 4 | Outdated Dependencies | Python-Funktion `analyze_outdated_dependencies()` mit Priority-Score: Security +100, Major +20, Age >365d +30, Releases-Behind x2. Sortiert nach priority_score | ~65 |
| 5 | Bundle Size Analysis | JavaScript `analyzeBundleSize()` via Bundlephobia API. Top-Offenders >1MB, Tree-Shaking-Empfehlungen | ~50 |
| 6 | Supply Chain Security | Python `check_supply_chain_security()`: Typosquatting (Levenshtein <=2), Maintainer-Changes, Suspicious Patterns | ~70 |
| 7 | Automated Remediation | Bash-Script: npm audit fix --force, pip-compile --upgrade-package. Python PR-Generator mit CVE-Tabelle | ~50 |
| 8 | Monitoring Setup | GitHub Actions Workflow: Daily cron, npm audit -> safety check -> license-checker -> Issue erstellen bei Critical | ~60 |

### Bewertung

| Aspekt | Urteil |
|--------|--------|
| Nuetzlicher Kern | Phasen 1-4 (Discovery, Vulnerabilities, Licenses, Outdated) + Phase 6 (Supply Chain) |
| Bloat | Vollstaendige Python-Klassen die ein LLM nicht ausfuehren kann — das sind Lehrbuch-Implementierungen, keine Anweisungen |
| Projektbewusstsein | Null — keine CLAUDE.md-Integration, keine CPM-Awareness |
| Ersetzt durch | `code-simplifier:deps-audit` (67 Zeilen) — fuehrt echte CLI-Tools aus, liest CLAUDE.md, kennt CPM/Version.props |
| Fazit | **Bereits ersetzt** |

---

## 4. reducing-entropy

**Minimizing Total Codebase Size** — 82 Zeilen + 4 References (213 Zeilen)
Pfad: `~/.claude/skills/reducing-entropy/SKILL.md`
Typ: User Skill, manual-only

### Kernprinzip

**"Was sieht die Codebase *danach* aus?"**

- 50 Zeilen schreiben die 200 loeschen = Gewinn
- 14 Funktionen behalten um 2 zu vermeiden = Verlust
- "Kein Churn" ist kein Ziel. Weniger Code ist das Ziel.

### Die 3 Fragen

| # | Frage | Pruefung |
|---|-------|----------|
| 1 | Was ist die kleinste Codebase die das loest? | Nicht kleinste Aenderung — kleinstes Ergebnis. 2 statt 14 Funktionen? 0 (Feature loeschen)? |
| 2 | Ergibt die Aenderung weniger Gesamtcode? | Zeilen vorher/nachher zaehlen. "Besser organisiert" + mehr Code = mehr Entropie |
| 3 | Was koennen wir loeschen? | Jede Aenderung ist Loeschgelegenheit. Was wird obsolet? Maximum entfernen. |

### Red Flags

| Gedanke | Problem |
|---------|---------|
| "Bestehendes behalten" | Status-Quo-Bias |
| "Fuegt Flexibilitaet hinzu" | YAGNI |
| "Bessere Separation of Concerns" | Mehr Files = mehr Code. Nicht gratis. |
| "Type Safety" | Wieviele Zeilen wert? |
| "Einfacher zu verstehen" | 14 Dinge != einfacher als 2 |

### Referenz-Mindsets

| Datei | Kern | Quelle |
|-------|------|--------|
| `simplicity-vs-easy.md` | Simple (objektiv, nicht verflochten) != Easy (subjektiv, vertraut). Wahle simple. "Complecting" = Verflechten = zukuenftige Debug-Sessions. | Rich Hickey "Simple Made Easy" |
| `data-over-abstractions.md` | 100 Funktionen auf 1 Datenstruktur > 10 auf 10. `Map<String,Value>` von Hunderten Funktionen verarbeitbar; `SettingsManager` nur von eigenen Methoden. Frage: Koennte das ein Map sein? | Rich Hickey "Value of Values", Mike Acton CppCon 2014 |
| `design-is-taking-apart.md` | Design = Dinge auseinandernehmen, nicht Features hinzufuegen. Jede Trennung reduziert, jede Kopplung erhoeht Komplexitaet. Inheritance complects, Composition liberates. | Moseley "Out of the Tar Pit", Ousterhout "Philosophy of Software Design" |
| `expensive-to-add-later.md` | PAGNI: YAGNI-Ausnahmen wenn Retrofit 10x+ teurer. Daten (timestamps, audit logs), Infrastruktur (API versioning, CI/CD), Security (vulnerability disclosure). Test: 10x teurer? Bekanntes Pattern? Kosten jetzt niedrig? | Simon Willison "PAGNIs", Jacob Kaplan-Moss "AppSec PAGNIs" |

### Bewertung

| Aspekt | Urteil |
|--------|--------|
| Qualitaet | Hoch — fokussiert, philosophisch fundiert, zero Bloat |
| Einzigartiger Wert | Die 4 Mindset-Dateien (Hickey, PAGNI, Data>Abstractions, Composition>Construction) existieren in keinem anderen Plugin |
| Ueberschneidung | Philosophie teilweise in `code-simplifier` ("Less code is better code", "Delete before abstracting"), aber die Mindsets als eigenstaendige Kalibrierungs-Referenz sind einzigartig |
| Fazit | **Behalten** — ergaenzt code-simplifier als philosophische Grundlage |

---

## Ueberschneidungsmatrix

| Faehigkeit | tech-debt | refactor-clean | deps-audit | reducing-entropy | code-simplifier | elegance-pipeline | exodia:hades |
|-----------|-----------|---------------|------------|-----------------|----------------|------------------|-------------|
| Debt Inventory | 5 Kategorien | Code Smells | — | — | — | Scouts | Dead Code |
| SOLID Tutorial | — | 180 Zeilen, 5 Sprachen | — | — | — | — | — |
| Eleganz-Metrik | KPI-Dashboard | Quality Checklist | — | Lines before/after | problem/solution ratio | Judge scoring | — |
| Loeschbias | — | "Remove dead code" | — | Kernphilosophie | "Delete before abstracting" | — | Kernphilosophie |
| Mindset-Referenzen | — | — | — | 4 Dateien | — | — | — |
| CLI-Tools ausfuehren | Nein | Nein | Nein | Nein | Nein | Ja | Ja |
| CLAUDE.md lesen | Nein | Nein | Nein | Nein | Ja | Ja | Ja |
| Sprache | Python/JS/YAML | Py/TS/Java/Go | Py/JS/Bash | Universal | Projekt-Stack | Projekt-Stack | Projekt-Stack |

---

## Empfehlungen

### Deinstallieren

| Plugin | Grund |
|--------|-------|
| `codebase-cleanup` (alle 3 Skills + 2 Agents) | Komplett ersetzt: deps-audit -> code-simplifier:deps-audit. tech-debt -> elegance-pipeline + exodia. refactor-clean -> code-simplifier Agent. |

### Behalten

| Skill | Grund |
|-------|-------|
| `reducing-entropy` (User Skill) | Einzigartige Mindset-Referenzen. Ergaenzt code-simplifier als philosophische Kalibrierung. |

### Optional: Mindsets in code-simplifier integrieren

Die 4 Referenz-Dateien aus reducing-entropy koennten als `references/` Ordner in das code-simplifier Plugin wandern. Dann haette der Agent direkten Zugriff auf die Mindsets als Entscheidungsgrundlage. reducing-entropy wuerde dann auch ueberfluessig.
