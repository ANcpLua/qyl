# Shadow Context Generator — JetBrains Plugin Design Spec

**Date:** 2026-03-12
**Status:** Draft
**Author:** ancplua + Claude

## Problem

AI coding agents operate inside a context window. When that window fills, the agent compacts
(summarizes and discards) earlier conversation. Critical decisions, file modifications, build
results, and debugging discoveries can be lost. The agent continues operating but with degraded
reasoning.

Every attempt to solve this from inside the agent's context window is parasitic on the agent
vendor's compaction implementation. If the vendor changes their JSONL format, summary logic,
or session lifecycle, the solution breaks. And it adds no new information — it only tries to
recover what was already there.

## Solution

A JetBrains IDE plugin that incrementally builds a rolling context document from IDE-first
signals. The document lives outside any agent's context window, is always current, and is
agent-agnostic. Any AI coding tool can consume it.

The plugin watches the IDE, not the agent. It captures signals that no terminal-based agent
has access to: file edits, build results, diagnostics, VCS operations, test results, editor
focus, and refactoring operations. These signals are maintained as a structured markdown
document at a known location in the project.

## Core Concept: Incremental Generator for Development Sessions

The architecture mirrors a Roslyn incremental source generator:

| Roslyn Generator | Shadow Context Generator |
|---|---|
| SyntaxProvider filters syntax nodes | IDE EventBus filters IDE events |
| Transform extracts a model | Transform extracts a state segment |
| Combine merges models | Combine merges segments |
| Emit writes `.g.cs` | Emit writes `context.md` |
| Incremental: only recompute what changed | Incremental: only recompute what changed |

Each IDE event updates one segment of the shadow. Old state is replaced, not appended.
The document never grows unboundedly — it is a snapshot, not a log.

## What the IDE Knows That No Agent Does

| IDE Signal | What It Captures | Agent Blind Spot |
|---|---|---|
| PSI/file edits | Which files changed, what lines, when | Agent sees diffs but loses them on compaction |
| Build events | Pass/fail, error messages, warnings | Agent ran `dotnet build` but the result is gone |
| Diagnostics | Real-time errors, analyzer warnings | Agent never had these — they are IDE-only |
| VCS operations | Commits, branch switches, stash | Agent ran `git` but context is compressed |
| Test results | Which tests ran, pass/fail, output | Agent ran tests but output was pruned |
| File navigation | What the human opened, focused on | Agent has no access to this — pure IDE signal |
| Refactorings | Renames, extract method, move | Agent does not know the IDE did this silently |

## Output

### Location

```
.ide/shadow/context.md
```

Deterministic, project-relative. Auto-added to `.gitignore`.

### Format: Markdown

Markdown is the output format because:
- Every AI agent can read it natively
- Human-readable without tooling
- No parsing library required
- Grep-friendly

### Example Output

```markdown
# Session Shadow — qyl
Generated: 2026-03-12T14:32:00Z | Segments: 7 active

## Current Focus
Files in editor: `OtlpConverter.cs`, `CapabilityEmitter.cs`
Last edited: `ServiceDefaultsSourceGenerator.cs` (2 min ago)

## Modified Files (this session)
- src/qyl.instrumentation.generators/Emitters/CapabilityEmitter.cs [NEW]
- src/qyl.instrumentation.generators/ServiceDefaultsSourceGenerator.cs [MODIFIED, 3 edits]
- src/qyl.collector/Ingestion/OtlpConverter.cs [MODIFIED, 1 edit]

## Build State
Last build: SUCCESS (12s ago)
Warnings: 0 | Errors: 0

## Diagnostics
No active errors or warnings in modified files.

## VCS State
Branch: feat/capability-manifests
Uncommitted changes: 3 files
Last commit: "feat: add compile-time capability manifests" (45 min ago)

## Test Results
Last run: 19 passed, 0 failed, 4 skipped
Filter: qyl.collector.tests

## Decisions & Context
- [14:30] Capability attributes travel via OTel Resource, not HTTP endpoints
- [14:15] Extracted GenAI/DB/Agent providers as standalone variables
- [auto] Renamed GeneratedTelemetryRegistration → GeneratedServiceRegistration
- [auto] Created CapabilityEmitter.cs
```

## Architecture

### Segment Pipeline

```
IDE Event / Listener                         Segment            Priority
────────────────────                         ───────            ────────
DocumentListener (EditorEventMulticaster) →  ModifiedFiles      HIGH
ProjectTaskListener.TOPIC (platform)     →   BuildState         HIGH
ProblemListener.TOPIC (platform)         →   Diagnostics        MEDIUM
BranchChangeListener.VCS_BRANCH_CHANGED  →   VcsState           HIGH
SMTRunnerEventsListener.TEST_STATUS [opt]→   TestResults        MEDIUM
FileEditorManagerListener.TOPIC          →   CurrentFocus       LOW
User hotkey (action)                     →   Decisions          CRITICAL
```

**Cross-IDE compatibility notes:**
- `ProjectTaskListener.TOPIC` (`com.intellij.task`) is platform-level and covers all
  build systems (MSBuild in Rider, Gradle, Maven, external tools). It replaces
  `CompilerTopics.COMPILATION_STATUS` which is Java-plugin-specific.
- `ProblemListener.TOPIC` (`com.intellij.analysis`) is platform-level and fires when
  inspection results change. It works across all IntelliJ-based IDEs.
- `SMTRunnerEventsListener` requires the `com.intellij.smRunner` module, declared as
  an optional dependency. When absent, the TestResults segment is disabled gracefully.

### Segment Interface

```kotlin
interface ContextSegment {
    val key: String
    val priority: Priority
    val lastUpdated: Instant
    val enabled: Boolean
    fun render(): String
    fun isEmpty(): Boolean
}

enum class Priority { CRITICAL, HIGH, MEDIUM, LOW }
```

Segments with `isEmpty() == true` are omitted from rendered output.

### Core Service

```kotlin
@Service(Level.PROJECT)
class ShadowContextService(private val project: Project) : Disposable {
    private val segments = ConcurrentHashMap<String, ContextSegment>()
    private var lastActivityTimestamp = Instant.now()

    fun updateSegment(segment: ContextSegment) {
        lastActivityTimestamp = Instant.now()
        segments[segment.key] = segment
        scheduleEmit()
    }

    // Debounced: coalesces rapid events (e.g., fast typing)
    private fun scheduleEmit() {
        // 2-second debounce (configurable via ShadowSettingsState)
        // Re-renders only if any segment actually changed
    }

    private fun emit() {
        // Snapshot segments to avoid concurrent modification during rendering
        val snapshot = HashMap(segments)
        val rendered = buildString {
            appendLine("# Session Shadow — ${project.name}")
            appendLine("Generated: ${Instant.now()} | Segments: ${activeCount(snapshot)} active")
            appendLine()
            snapshot.values
                .filter { it.enabled && !it.isEmpty() }
                .sortedByDescending { it.priority }
                .forEach { segment ->
                    appendLine(segment.render())
                    appendLine()
                }
        }
        writeShadowFile(rendered)
    }

    override fun dispose() {
        // Cleanup: cancel pending debounce timers
    }
}
```

**Concurrency note:** `emit()` takes a `HashMap` snapshot of `segments` at entry to avoid
reading a mix of old and new segments if `updateSegment()` fires concurrently.

### Event Collector

```kotlin
class IdeEventCollector(private val project: Project) : ProjectActivity {
    override suspend fun execute(project: Project) {
        val service = project.service<ShadowContextService>()
        val connection = project.messageBus.connect(service)

        // File edits — application-level listener, must filter by project
        EditorFactory.getInstance().eventMulticaster
            .addDocumentListener(ShadowDocumentListener(project, service), service)

        // Build results — platform-level topic, works across all IDEs
        connection.subscribe(ProjectTaskListener.TOPIC, ShadowBuildListener(service))

        // Diagnostics — platform-level, fires on inspection result changes
        connection.subscribe(ProblemListener.TOPIC, ShadowDiagnosticsListener(project, service))

        // VCS operations
        connection.subscribe(BranchChangeListener.VCS_BRANCH_CHANGED, ShadowVcsListener(service))

        // File editor focus
        connection.subscribe(FileEditorManagerListener.FILE_EDITOR_MANAGER, ShadowFocusListener(service))

        // Test results — optional dependency on smRunner module
        trySubscribeTestListener(connection, service)
    }

    private fun trySubscribeTestListener(connection: MessageBusConnection, service: ShadowContextService) {
        try {
            connection.subscribe(SMTRunnerEventsListener.TEST_STATUS, ShadowTestListener(service))
        } catch (_: Exception) {
            // smRunner module not available in this IDE — TestResults segment stays empty
        }
    }
}
```

**Cross-project filtering:** `ShadowDocumentListener` receives `project` and checks
`ProjectFileIndex.getInstance(project).isInContent(file)` before updating segments.
Documents belonging to other open projects are ignored.

**Disposable lifecycle:** All listeners use `service` (which implements `Disposable`)
as the parent disposable. When the project closes, all listeners are automatically
unregistered.

### User Annotation: Pin to Shadow

A keyboard shortcut (no default binding — user assigns via `Settings → Keymap → Shadow Context → Pin to Shadow`) opens a small popup dialog:

```
┌─────────────────────────────────────────┐
│  Pin to Shadow                          │
│                                         │
│ [text field for decision/note]          │
│                                         │
│              [Pin]  [Cancel]            │
└─────────────────────────────────────────┘
```

Pinned notes go into the Decisions segment with a timestamp. Limited to 10 most recent
pins (FIFO). This is the human's escape hatch — the one thing no automated collector can
infer: why a decision was made.

### Auto-Detected Decisions

Beyond manual pins, the Decisions segment auto-detects significant operations:

- Git commit messages → "committed: feat: add capability manifests"
- Rename refactorings → "renamed Foo → Bar"
- File creation/deletion → "created CapabilityEmitter.cs"
- Branch switch → "switched to feat/shadow-plugin"

Auto-entries have lower priority than manual pins. Capped at 5 most recent.

## Segment Types

### ModifiedFilesSegment

Tracks files modified during the current session. Records file path, modification type
(NEW, MODIFIED, DELETED), and edit count. Capped at configurable max entries (default 20).
Oldest entries are evicted first.

### BuildStateSegment

Captures last build result: success/failure, error count, warning count, time since build.
On failure, includes up to 5 error messages with file/line references.

### DiagnosticsSegment

Captures current IDE diagnostics (errors, warnings) in files that were modified during the
session. Does not report diagnostics in untouched files — only what is relevant to current
work.

### VcsStateSegment

Current branch, uncommitted change count, last commit message and timestamp, stash count.
Updated on branch switch, commit, or stash operations.

### TestResultsSegment

Last test run results: pass/fail/skip counts, failed test names (up to 10), test filter
used, time since run.

### CurrentFocusSegment

Currently open editor tabs and last-focused file. Updated on editor tab switch. Low priority
— useful for understanding what the human is looking at but not critical for context recovery.

### DecisionsSegment

Manual pins (user-annotated) and auto-detected significant operations. Manual pins are
CRITICAL priority. Auto-detected entries are HIGH priority. FIFO with configurable cap
(default: 10 manual + 5 auto).

## Configuration

Settings path: `Settings → Tools → Shadow Context`

| Setting | Default | Purpose |
|---|---|---|
| Output path | `.ide/shadow/context.md` | Where the shadow file lives |
| Auto-gitignore | `true` | Adds `.ide/shadow/` to `.gitignore` on first run |
| Segment toggles | All ON | Enable/disable individual segments |
| Max file entries | `20` | Cap on Modified Files segment |
| Emit debounce | `2s` | Coalesce rapid events before re-rendering |
| Session timeout | `30min` | Reset shadow after inactivity (see Session Lifecycle) |
| Decision hotkey | *(unbound)* | Pin a decision — user assigns via Keymap settings |
| Max manual pins | `10` | FIFO cap on manual decisions |
| Max auto decisions | `5` | FIFO cap on auto-detected decisions |

Settings are per-project, stored via `PersistentStateComponent` with
`@State(storages = [Storage("shadow-context.xml")])` in the project's `.idea/` directory.
Different projects can have different shadow configurations (output path, enabled segments,
caps).

## Session Lifecycle

A "session" is a continuous period of developer activity within a project.

### Session Start

A session begins when the plugin detects the first user-initiated IDE event after project
open or after a session reset. Background events (reindexing, auto-save, daemon analyzer
warmup) do not start a session.

User-initiated events: document edits via editor, explicit build trigger, VCS operations,
file open/close, test execution, Pin to Shadow action.

### Session Timeout and Reset

The session timeout timer resets on every user-initiated event. If no user-initiated event
occurs for the configured timeout period (default 30 minutes):

1. The current shadow file is archived to `.ide/shadow/previous.md` (single-slot,
   overwritten each time — not unbounded history).
2. All segments are cleared.
3. Manual pins are preserved in the archived file but cleared from the active shadow.
4. The next user-initiated event starts a new session.

This means: returning after 29 minutes of idle, making one edit, then going idle again
for 30 minutes will produce a session containing only that single edit. The timer resets
on every qualifying event.

### Project Close

On project close, the current shadow is written to disk as-is. On next project open,
if the shadow file exists and is younger than the session timeout, the session resumes.
Otherwise, it archives and starts fresh.

## Tool Window: Shadow Preview

An optional tool window (`View → Tool Windows → Shadow Context`) displays a live preview
of the current `context.md` content. It is hidden by default and does not auto-activate.

The tool window serves two purposes:
1. Lets the developer see what the shadow currently contains without opening the file.
2. Provides a "Copy to Clipboard" button for manual paste into a terminal agent.

The tool window re-renders on every `emit()` cycle. It is a read-only view — editing
happens through IDE actions (file edits, builds, pins), not through the tool window.

## Agent Consumption

The plugin does not inject, paste, or hook into any agent. It maintains a file. Agents
consume it through their native mechanisms:

| Agent | How It Reads the Shadow |
|---|---|
| Claude Code | `Read .ide/shadow/context.md` or CLAUDE.md rule referencing it |
| GitHub Copilot | `.github/copilot-instructions.md` references it |
| Cursor | `.cursorrules` references it |
| Any agent | The file is in the project directory, always current |

For compaction recovery: the human can paste the shadow contents into the terminal. But
the agent can also read the file directly — it is always there. No special recovery logic
needed. The shadow IS the recovery.

## Project Structure

```
qyl-shadow-plugin/
├── gradle/libs.versions.toml
├── build.gradle.kts
├── gradle.properties
├── settings.gradle.kts
├── src/main/
│   ├── kotlin/dev/qyl/shadow/
│   │   ├── ShadowStartupActivity.kt
│   │   ├── ShadowContextService.kt
│   │   ├── IdeEventCollector.kt
│   │   ├── ShadowSettingsState.kt
│   │   ├── ShadowSettingsConfigurable.kt
│   │   ├── ShadowToolWindow.kt
│   │   ├── PinToShadowAction.kt
│   │   └── segments/
│   │       ├── ContextSegment.kt
│   │       ├── ModifiedFilesSegment.kt
│   │       ├── BuildStateSegment.kt
│   │       ├── DiagnosticsSegment.kt
│   │       ├── VcsStateSegment.kt
│   │       ├── TestResultsSegment.kt
│   │       ├── CurrentFocusSegment.kt
│   │       └── DecisionsSegment.kt
│   └── resources/META-INF/
│       └── plugin.xml
└── src/test/
    └── kotlin/dev/qyl/shadow/
        ├── ShadowContextServiceTest.kt
        └── segments/
            └── ModifiedFilesSegmentTest.kt
```

Scaffolded from `JetBrains/intellij-platform-plugin-template`. No .NET backend. No RdGen
protocol. Pure Kotlin frontend plugin.

## Marketplace Listing

- **Name**: Shadow Context
- **Category**: Tools Integration
- **Compatibility**: All IntelliJ-based IDEs (Rider, IntelliJ IDEA, WebStorm, PyCharm, GoLand, etc.)
- **Size**: <1MB
- **Tagline**: "IDE-native context that survives agent memory loss. Works with any AI coding tool."

## Dependencies

### Required (platform core)

- `com.intellij.modules.platform` — base IntelliJ Platform

### Optional (graceful degradation)

- `com.intellij.smRunner` — test result listener. When absent, TestResultsSegment
  is disabled. The plugin logs a one-time info message and continues.

All other event sources (`ProjectTaskListener`, `ProblemListener`, `BranchChangeListener`,
`FileEditorManagerListener`) are part of the platform core and available in every
IntelliJ-based IDE.

## Constraints

- No dependency on any specific AI agent or vendor
- No network calls — purely local file I/O
- No .NET backend — pure Kotlin, runs on all IntelliJ-based IDEs
- No runtime overhead when no segments are changing (event-driven, not polling)
- Shadow file is gitignored by default — no accidental commits of session state
- Plugin must work without configuration — zero-config defaults produce useful output
- All IDE-specific modules beyond platform core are optional dependencies with
  graceful degradation

## Future Considerations (Out of Scope for V1)

- **Segment plugins**: allow third-party extensions to add custom segments
- **Shadow history**: keep last N snapshots for diffing (what changed in the shadow)
- **Team shadows**: aggregate shadows from multiple developers for team context
- **MCP tool**: expose shadow as an MCP resource so agents can subscribe to changes
- **qyl integration**: push shadow metadata as OTel Resource attributes alongside
  capability manifests
