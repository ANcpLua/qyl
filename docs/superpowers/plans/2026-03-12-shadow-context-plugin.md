# Shadow Context Plugin — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a JetBrains plugin that incrementally generates a rolling markdown context document from IDE events, enabling any AI coding agent to recover context after compaction.

**Architecture:** Event-driven segment pipeline. IDE events (file edits, builds, diagnostics, VCS, tests, focus) flow through listeners into typed segments. Segments render to markdown. A debounced emitter writes the snapshot to `.ide/shadow/context.md`. Pure Kotlin, no .NET backend.

**Tech Stack:** Kotlin 2.3, IntelliJ Platform SDK 2025.2, Gradle 9.3.1, IntelliJ Platform Gradle Plugin 2.11.0, JUnit 4

**Spec:** `docs/superpowers/specs/2026-03-12-shadow-context-plugin-design.md`

---

## File Map

All source lives in a NEW repo scaffolded from `JetBrains/intellij-platform-plugin-template` at `/Users/ancplua/shadow-context-plugin/`. Package: `dev.qyl.shadow`.

```
shadow-context-plugin/
├── gradle.properties                          # Plugin metadata, platform version
├── build.gradle.kts                           # Build config (adapted from template)
├── settings.gradle.kts                        # Root project name
├── gradle/libs.versions.toml                  # Version catalog
├── src/main/kotlin/dev/qyl/shadow/
│   ├── ShadowBundle.kt                        # Message bundle for i18n
│   ├── ShadowStartupActivity.kt               # ProjectActivity — registers event collector
│   ├── ShadowContextService.kt                # Core: segment registry, debounced emit via Alarm, session timeout
│   ├── ShadowContextRenderer.kt               # Pure function: segments → markdown (no IDE deps)
│   ├── IdeEventCollector.kt                   # Subscribes to all IDE event topics, wires auto-decisions
│   ├── ShadowSettingsState.kt                 # PersistentStateComponent — per-project settings
│   ├── ShadowSettingsConfigurable.kt          # Settings UI panel
│   ├── ShadowToolWindow.kt                    # ToolWindowFactory — live preview + copy button
│   ├── PinToShadowAction.kt                   # AnAction — keyboard shortcut to pin decisions
│   ├── ShadowCheckinHandlerFactory.kt         # VCS commit hook → auto-decision + VcsState
│   └── segments/
│       ├── ContextSegment.kt                  # Interface + Priority enum
│       ├── ModifiedFilesSegment.kt            # Tracks edited files
│       ├── BuildStateSegment.kt               # Last build result
│       ├── DiagnosticsSegment.kt              # IDE diagnostics in modified files
│       ├── VcsStateSegment.kt                 # Branch, commits, uncommitted changes
│       ├── TestResultsSegment.kt              # Test pass/fail/skip
│       ├── CurrentFocusSegment.kt             # Open editor tabs
│       └── DecisionsSegment.kt                # Manual pins + auto-detected decisions
├── src/main/resources/
│   ├── META-INF/plugin.xml                    # Plugin descriptor
│   └── messages/ShadowBundle.properties       # UI strings
└── src/test/kotlin/dev/qyl/shadow/
    ├── ShadowContextRendererTest.kt            # Renderer logic (pure, no IDE deps)
    │                                           # Note: ShadowContextService is not unit-tested in V1 —
    │                                           # requires HeavyPlatformTestCase (IDE infrastructure).
    │                                           # Renderer extraction makes the core logic testable without IDE deps.
    └── segments/
        ├── ModifiedFilesSegmentTest.kt
        ├── BuildStateSegmentTest.kt
        ├── DiagnosticsSegmentTest.kt
        ├── TestResultsSegmentTest.kt
        ├── CurrentFocusSegmentTest.kt
        ├── DecisionsSegmentTest.kt
        └── VcsStateSegmentTest.kt
```

---

## Chunk 1: Project Scaffold + Segment Interface

### Task 1: Scaffold project from template

**Files:**
- Create: `shadow-context-plugin/` (entire project tree from template)

- [ ] **Step 1: Create repo from template**

```bash
cd /Users/ancplua
gh repo create ancplua/shadow-context-plugin --template JetBrains/intellij-platform-plugin-template --clone --public
cd shadow-context-plugin
```

- [ ] **Step 2: Update gradle.properties**

Replace template values:

```properties
pluginGroup = dev.qyl.shadow
pluginName = Shadow Context
pluginRepositoryUrl = https://github.com/ancplua/shadow-context-plugin
pluginVersion = 0.1.0

pluginSinceBuild = 252
platformVersion = 2025.2.5

platformPlugins =
platformBundledPlugins =
platformBundledModules =

gradleVersion = 9.3.1
kotlin.stdlib.default.dependency = false
org.gradle.configuration-cache = true
org.gradle.caching = true
```

- [ ] **Step 3: Update settings.gradle.kts**

```kotlin
rootProject.name = "Shadow Context"

plugins {
    id("org.gradle.toolchains.foojay-resolver-convention") version "1.0.0"
}
```

- [ ] **Step 4: Delete template sample code**

Delete:
- `src/main/kotlin/org/jetbrains/plugins/template/` (entire directory)
- `src/test/kotlin/org/jetbrains/plugins/template/` (entire directory)
- `src/main/resources/messages/MyBundle.properties`

- [ ] **Step 5: Create package directories**

```bash
mkdir -p src/main/kotlin/dev/qyl/shadow/segments
mkdir -p src/test/kotlin/dev/qyl/shadow/segments
```

- [ ] **Step 6: Build to verify scaffold**

```bash
./gradlew build
```

Expected: BUILD SUCCESSFUL (no source files yet, but Gradle config is valid)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "chore: scaffold Shadow Context plugin from JetBrains template"
```

---

### Task 2: ContextSegment interface and Priority enum

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/segments/ContextSegment.kt`
- Test: `src/test/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegmentTest.kt` (placeholder)

- [ ] **Step 1: Write ContextSegment interface**

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/ContextSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant

enum class Priority { CRITICAL, HIGH, MEDIUM, LOW }

interface ContextSegment {
    val key: String
    val priority: Priority
    val lastUpdated: Instant
    val enabled: Boolean
    fun render(): String
    fun isEmpty(): Boolean
}
```

- [ ] **Step 2: Build to verify**

```bash
./gradlew build
```

Expected: BUILD SUCCESSFUL

- [ ] **Step 3: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/segments/ContextSegment.kt
git commit -m "feat: add ContextSegment interface and Priority enum"
```

---

### Task 3: ModifiedFilesSegment

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegment.kt`
- Create: `src/test/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegmentTest.kt`

- [ ] **Step 1: Write the failing test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class ModifiedFilesSegmentTest {

    @Test
    fun `empty segment renders nothing`() {
        val segment = ModifiedFilesSegment(maxEntries = 20)
        assertTrue(segment.isEmpty())
        assertEquals("", segment.render())
    }

    @Test
    fun `tracks new file`() {
        val segment = ModifiedFilesSegment(maxEntries = 20)
        segment.recordEdit("src/Foo.kt", isNew = true)
        assertFalse(segment.isEmpty())
        val rendered = segment.render()
        assertTrue(rendered.contains("[NEW]"))
        assertTrue(rendered.contains("src/Foo.kt"))
    }

    @Test
    fun `tracks modified file with edit count`() {
        val segment = ModifiedFilesSegment(maxEntries = 20)
        segment.recordEdit("src/Bar.kt", isNew = false)
        segment.recordEdit("src/Bar.kt", isNew = false)
        segment.recordEdit("src/Bar.kt", isNew = false)
        val rendered = segment.render()
        assertTrue(rendered.contains("[MODIFIED, 3 edits]"))
    }

    @Test
    fun `tracks deleted file`() {
        val segment = ModifiedFilesSegment(maxEntries = 20)
        segment.recordDeletion("src/Old.kt")
        val rendered = segment.render()
        assertTrue(rendered.contains("[DELETED]"))
    }

    @Test
    fun `evicts oldest entries when cap exceeded`() {
        val segment = ModifiedFilesSegment(maxEntries = 2)
        segment.recordEdit("src/A.kt", isNew = false)
        segment.recordEdit("src/B.kt", isNew = false)
        segment.recordEdit("src/C.kt", isNew = false)
        val rendered = segment.render()
        assertFalse(rendered.contains("src/A.kt"))
        assertTrue(rendered.contains("src/B.kt"))
        assertTrue(rendered.contains("src/C.kt"))
    }

    @Test
    fun `clear resets segment`() {
        val segment = ModifiedFilesSegment(maxEntries = 20)
        segment.recordEdit("src/Foo.kt", isNew = true)
        segment.clear()
        assertTrue(segment.isEmpty())
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
./gradlew test --tests "dev.qyl.shadow.segments.ModifiedFilesSegmentTest"
```

Expected: FAIL — `ModifiedFilesSegment` not found

- [ ] **Step 3: Implement ModifiedFilesSegment**

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant
import java.util.LinkedHashMap

enum class FileModType { NEW, MODIFIED, DELETED }

data class FileEntry(
    val path: String,
    val type: FileModType,
    val editCount: Int,
    val firstSeen: Instant,
)

class ModifiedFilesSegment(private val maxEntries: Int) : ContextSegment {
    override val key = "modified-files"
    override val priority = Priority.HIGH
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private val entries = LinkedHashMap<String, FileEntry>(maxEntries + 1, 0.75f, false)

    fun recordEdit(path: String, isNew: Boolean) {
        lastUpdated = Instant.now()
        val existing = entries[path]
        if (existing != null) {
            entries[path] = existing.copy(editCount = existing.editCount + 1, type = existing.type)
        } else {
            evictIfNeeded()
            entries[path] = FileEntry(
                path = path,
                type = if (isNew) FileModType.NEW else FileModType.MODIFIED,
                editCount = 1,
                firstSeen = Instant.now(),
            )
        }
    }

    fun recordDeletion(path: String) {
        lastUpdated = Instant.now()
        evictIfNeeded()
        entries[path] = FileEntry(
            path = path,
            type = FileModType.DELETED,
            editCount = 0,
            firstSeen = Instant.now(),
        )
    }

    fun clear() {
        entries.clear()
    }

    fun getFilePaths(): Set<String> = entries.keys.toSet()

    override fun isEmpty(): Boolean = entries.isEmpty()

    override fun render(): String {
        if (isEmpty()) return ""
        return buildString {
            appendLine("## Modified Files (this session)")
            for ((_, entry) in entries) {
                val tag = when (entry.type) {
                    FileModType.NEW -> "[NEW]"
                    FileModType.MODIFIED -> "[MODIFIED, ${entry.editCount} edits]"
                    FileModType.DELETED -> "[DELETED]"
                }
                appendLine("- ${entry.path} $tag")
            }
        }.trimEnd()
    }

    private fun evictIfNeeded() {
        while (entries.size >= maxEntries) {
            val oldest = entries.keys.firstOrNull() ?: break
            entries.remove(oldest)
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
./gradlew test --tests "dev.qyl.shadow.segments.ModifiedFilesSegmentTest"
```

Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegment.kt \
        src/test/kotlin/dev/qyl/shadow/segments/ModifiedFilesSegmentTest.kt
git commit -m "feat: add ModifiedFilesSegment with FIFO eviction"
```

---

### Task 4: Remaining segments (BuildState, Diagnostics, VCS, TestResults, CurrentFocus, Decisions)

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/segments/BuildStateSegment.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/segments/DiagnosticsSegment.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/segments/VcsStateSegment.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/segments/TestResultsSegment.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/segments/CurrentFocusSegment.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/segments/DecisionsSegment.kt`
- Create: `src/test/kotlin/dev/qyl/shadow/segments/BuildStateSegmentTest.kt`
- Create: `src/test/kotlin/dev/qyl/shadow/segments/DecisionsSegmentTest.kt`
- Create: `src/test/kotlin/dev/qyl/shadow/segments/VcsStateSegmentTest.kt`

- [ ] **Step 1: Write BuildStateSegment test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/BuildStateSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class BuildStateSegmentTest {

    @Test
    fun `empty before any build`() {
        val segment = BuildStateSegment()
        assertTrue(segment.isEmpty())
    }

    @Test
    fun `renders success`() {
        val segment = BuildStateSegment()
        segment.recordBuild(success = true, errors = emptyList(), warningCount = 0)
        val rendered = segment.render()
        assertTrue(rendered.contains("SUCCESS"))
        assertTrue(rendered.contains("Errors: 0"))
    }

    @Test
    fun `renders failure with errors capped at 5`() {
        val segment = BuildStateSegment()
        val errors = (1..8).map { "Error $it at Foo.kt:$it" }
        segment.recordBuild(success = false, errors = errors, warningCount = 2)
        val rendered = segment.render()
        assertTrue(rendered.contains("FAILURE"))
        assertTrue(rendered.contains("Error 5"))
        assertFalse(rendered.contains("Error 6"))
        assertTrue(rendered.contains("Warnings: 2"))
    }
}
```

- [ ] **Step 2: Write DecisionsSegment test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/DecisionsSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class DecisionsSegmentTest {

    @Test
    fun `empty with no pins`() {
        val segment = DecisionsSegment(maxManualPins = 3, maxAutoDecisions = 2)
        assertTrue(segment.isEmpty())
    }

    @Test
    fun `manual pin renders with timestamp`() {
        val segment = DecisionsSegment(maxManualPins = 3, maxAutoDecisions = 2)
        segment.addManualPin("Use OTel Resource, not HTTP")
        val rendered = segment.render()
        assertTrue(rendered.contains("Use OTel Resource, not HTTP"))
        assertFalse(segment.isEmpty())
    }

    @Test
    fun `manual pins evict oldest when cap exceeded`() {
        val segment = DecisionsSegment(maxManualPins = 2, maxAutoDecisions = 2)
        segment.addManualPin("First")
        segment.addManualPin("Second")
        segment.addManualPin("Third")
        val rendered = segment.render()
        assertFalse(rendered.contains("First"))
        assertTrue(rendered.contains("Second"))
        assertTrue(rendered.contains("Third"))
    }

    @Test
    fun `auto decisions separate from manual`() {
        val segment = DecisionsSegment(maxManualPins = 3, maxAutoDecisions = 2)
        segment.addManualPin("Manual note")
        segment.addAutoDecision("committed: feat: add X")
        val rendered = segment.render()
        assertTrue(rendered.contains("Manual note"))
        assertTrue(rendered.contains("[auto]"))
        assertTrue(rendered.contains("committed: feat: add X"))
    }

    @Test
    fun `clear removes all entries`() {
        val segment = DecisionsSegment(maxManualPins = 3, maxAutoDecisions = 2)
        segment.addManualPin("Note")
        segment.addAutoDecision("Auto")
        segment.clear()
        assertTrue(segment.isEmpty())
    }
}
```

- [ ] **Step 3: Write VcsStateSegment test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/VcsStateSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class VcsStateSegmentTest {

    @Test
    fun `empty before any VCS event`() {
        val segment = VcsStateSegment()
        assertTrue(segment.isEmpty())
    }

    @Test
    fun `renders branch and uncommitted count`() {
        val segment = VcsStateSegment()
        segment.updateBranch("feat/shadow")
        segment.updateUncommittedCount(3)
        val rendered = segment.render()
        assertTrue(rendered.contains("feat/shadow"))
        assertTrue(rendered.contains("3"))
    }

    @Test
    fun `renders last commit`() {
        val segment = VcsStateSegment()
        segment.updateBranch("main")
        segment.updateLastCommit("fix: resolve null ref")
        val rendered = segment.render()
        assertTrue(rendered.contains("fix: resolve null ref"))
    }
}
```

- [ ] **Step 4: Run all tests to verify they fail**

```bash
./gradlew test
```

Expected: FAIL — segment classes not found

- [ ] **Step 5: Implement BuildStateSegment**

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/BuildStateSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant

class BuildStateSegment : ContextSegment {
    override val key = "build-state"
    override val priority = Priority.HIGH
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private var success: Boolean? = null
    private var errors: List<String> = emptyList()
    private var warningCount: Int = 0

    fun recordBuild(success: Boolean, errors: List<String>, warningCount: Int) {
        this.success = success
        this.errors = errors
        this.warningCount = warningCount
        lastUpdated = Instant.now()
    }

    fun clear() {
        success = null
        errors = emptyList()
        warningCount = 0
    }

    override fun isEmpty(): Boolean = success == null

    override fun render(): String {
        val s = success ?: return ""
        return buildString {
            appendLine("## Build State")
            appendLine("Last build: ${if (s) "SUCCESS" else "FAILURE"}")
            appendLine("Warnings: $warningCount | Errors: ${errors.size}")
            if (!s && errors.isNotEmpty()) {
                appendLine()
                errors.take(5).forEach { appendLine("- $it") }
            }
        }.trimEnd()
    }
}
```

- [ ] **Step 6: Implement VcsStateSegment**

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/VcsStateSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant

class VcsStateSegment : ContextSegment {
    override val key = "vcs-state"
    override val priority = Priority.HIGH
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private var branch: String? = null
    private var uncommittedCount: Int = 0
    private var lastCommitMessage: String? = null

    fun updateBranch(name: String) {
        branch = name
        lastUpdated = Instant.now()
    }

    fun updateUncommittedCount(count: Int) {
        uncommittedCount = count
        lastUpdated = Instant.now()
    }

    fun updateLastCommit(message: String) {
        lastCommitMessage = message
        lastUpdated = Instant.now()
    }

    fun clear() {
        branch = null
        uncommittedCount = 0
        lastCommitMessage = null
    }

    override fun isEmpty(): Boolean = branch == null

    override fun render(): String {
        val b = branch ?: return ""
        return buildString {
            appendLine("## VCS State")
            appendLine("Branch: $b")
            appendLine("Uncommitted changes: $uncommittedCount")
            if (lastCommitMessage != null) {
                appendLine("Last commit: \"$lastCommitMessage\"")
            }
        }.trimEnd()
    }
}
```

- [ ] **Step 7: Implement DecisionsSegment**

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/DecisionsSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant
import java.time.LocalTime
import java.time.ZoneId

data class Decision(val text: String, val timestamp: Instant, val isAuto: Boolean)

class DecisionsSegment(
    private val maxManualPins: Int,
    private val maxAutoDecisions: Int,
) : ContextSegment {
    override val key = "decisions"
    override val priority = Priority.CRITICAL
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private val manualPins = ArrayDeque<Decision>()
    private val autoDecisions = ArrayDeque<Decision>()

    fun addManualPin(text: String) {
        lastUpdated = Instant.now()
        manualPins.addLast(Decision(text, Instant.now(), isAuto = false))
        while (manualPins.size > maxManualPins) manualPins.removeFirst()
    }

    fun addAutoDecision(text: String) {
        lastUpdated = Instant.now()
        autoDecisions.addLast(Decision(text, Instant.now(), isAuto = true))
        while (autoDecisions.size > maxAutoDecisions) autoDecisions.removeFirst()
    }

    fun clear() {
        manualPins.clear()
        autoDecisions.clear()
    }

    override fun isEmpty(): Boolean = manualPins.isEmpty() && autoDecisions.isEmpty()

    override fun render(): String {
        if (isEmpty()) return ""
        return buildString {
            appendLine("## Decisions & Context")
            for (pin in manualPins) {
                val time = LocalTime.ofInstant(pin.timestamp, ZoneId.systemDefault())
                    .format(java.time.format.DateTimeFormatter.ofPattern("HH:mm"))
                appendLine("- [$time] ${pin.text}")
            }
            for (auto in autoDecisions) {
                appendLine("- [auto] ${auto.text}")
            }
        }.trimEnd()
    }
}
```

- [ ] **Step 8: Implement remaining segments (DiagnosticsSegment, TestResultsSegment, CurrentFocusSegment)**

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/DiagnosticsSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant

data class DiagnosticEntry(val file: String, val severity: String, val message: String)

class DiagnosticsSegment : ContextSegment {
    override val key = "diagnostics"
    override val priority = Priority.MEDIUM
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private val entries = mutableListOf<DiagnosticEntry>()
    private var hasBeenUpdated = false

    fun update(diagnostics: List<DiagnosticEntry>) {
        entries.clear()
        entries.addAll(diagnostics)
        hasBeenUpdated = true
        lastUpdated = Instant.now()
    }

    fun clear() {
        entries.clear()
        hasBeenUpdated = false
    }

    // Not empty once updated — shows "no issues" message when clean
    override fun isEmpty(): Boolean = !hasBeenUpdated

    override fun render(): String {
        if (!hasBeenUpdated) return ""
        if (entries.isEmpty()) return "## Diagnostics\nNo active errors or warnings in modified files."
        return buildString {
            appendLine("## Diagnostics")
            for (entry in entries.take(10)) {
                appendLine("- ${entry.severity}: ${entry.message} (${entry.file})")
            }
        }.trimEnd()
    }
}
```

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/TestResultsSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant

class TestResultsSegment : ContextSegment {
    override val key = "test-results"
    override val priority = Priority.MEDIUM
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private var passed: Int = 0
    private var failed: Int = 0
    private var skipped: Int = 0
    private var failedNames: List<String> = emptyList()
    private var hasResults = false

    fun recordResults(passed: Int, failed: Int, skipped: Int, failedNames: List<String>) {
        this.passed = passed
        this.failed = failed
        this.skipped = skipped
        this.failedNames = failedNames
        this.hasResults = true
        lastUpdated = Instant.now()
    }

    fun clear() {
        hasResults = false
        passed = 0; failed = 0; skipped = 0
        failedNames = emptyList()
    }

    override fun isEmpty(): Boolean = !hasResults

    override fun render(): String {
        if (!hasResults) return ""
        return buildString {
            appendLine("## Test Results")
            appendLine("Last run: $passed passed, $failed failed, $skipped skipped")
            if (failedNames.isNotEmpty()) {
                appendLine("Failed:")
                failedNames.take(10).forEach { appendLine("- $it") }
            }
        }.trimEnd()
    }
}
```

```kotlin
// src/main/kotlin/dev/qyl/shadow/segments/CurrentFocusSegment.kt
package dev.qyl.shadow.segments

import java.time.Instant

class CurrentFocusSegment : ContextSegment {
    override val key = "current-focus"
    override val priority = Priority.LOW
    override var lastUpdated: Instant = Instant.now()
        private set
    override var enabled: Boolean = true

    private val openFiles = LinkedHashSet<String>()
    private var lastEdited: String? = null

    fun recordFileOpened(path: String) {
        openFiles.add(path)
        lastUpdated = Instant.now()
    }

    fun recordFileClosed(path: String) {
        openFiles.remove(path)
        lastUpdated = Instant.now()
    }

    fun recordLastEdited(path: String) {
        lastEdited = path
        lastUpdated = Instant.now()
    }

    fun clear() {
        openFiles.clear()
        lastEdited = null
    }

    override fun isEmpty(): Boolean = openFiles.isEmpty()

    override fun render(): String {
        if (isEmpty()) return ""
        return buildString {
            appendLine("## Current Focus")
            appendLine("Files in editor: ${openFiles.joinToString(", ") { "`$it`" }}")
            if (lastEdited != null) {
                appendLine("Last edited: `$lastEdited`")
            }
        }.trimEnd()
    }
}
```

- [ ] **Step 9: Write DiagnosticsSegment test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/DiagnosticsSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class DiagnosticsSegmentTest {

    @Test
    fun `empty before any update`() {
        val segment = DiagnosticsSegment()
        assertTrue(segment.isEmpty())
        assertEquals("", segment.render())
    }

    @Test
    fun `shows no issues message when updated with empty list`() {
        val segment = DiagnosticsSegment()
        segment.update(emptyList())
        assertFalse(segment.isEmpty())
        assertTrue(segment.render().contains("No active errors or warnings"))
    }

    @Test
    fun `renders diagnostics entries`() {
        val segment = DiagnosticsSegment()
        segment.update(listOf(
            DiagnosticEntry("Foo.kt", "ERROR", "Unresolved reference 'bar'"),
            DiagnosticEntry("Baz.kt", "WARNING", "Unused import"),
        ))
        val rendered = segment.render()
        assertTrue(rendered.contains("ERROR"))
        assertTrue(rendered.contains("Unresolved reference"))
        assertTrue(rendered.contains("WARNING"))
    }

    @Test
    fun `caps entries at 10`() {
        val segment = DiagnosticsSegment()
        val entries = (1..15).map { DiagnosticEntry("File$it.kt", "ERROR", "Error $it") }
        segment.update(entries)
        val rendered = segment.render()
        assertTrue(rendered.contains("Error 10"))
        assertFalse(rendered.contains("Error 11"))
    }

    @Test
    fun `clear resets to empty`() {
        val segment = DiagnosticsSegment()
        segment.update(listOf(DiagnosticEntry("Foo.kt", "ERROR", "oops")))
        segment.clear()
        assertTrue(segment.isEmpty())
    }
}
```

- [ ] **Step 10: Write TestResultsSegment test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/TestResultsSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class TestResultsSegmentTest {

    @Test
    fun `empty before any results`() {
        val segment = TestResultsSegment()
        assertTrue(segment.isEmpty())
        assertEquals("", segment.render())
    }

    @Test
    fun `renders pass fail skip counts`() {
        val segment = TestResultsSegment()
        segment.recordResults(passed = 10, failed = 2, skipped = 1, failedNames = listOf("testA", "testB"))
        val rendered = segment.render()
        assertTrue(rendered.contains("10 passed"))
        assertTrue(rendered.contains("2 failed"))
        assertTrue(rendered.contains("1 skipped"))
    }

    @Test
    fun `renders failed test names capped at 10`() {
        val segment = TestResultsSegment()
        val names = (1..15).map { "failedTest$it" }
        segment.recordResults(passed = 0, failed = 15, skipped = 0, failedNames = names)
        val rendered = segment.render()
        assertTrue(rendered.contains("failedTest10"))
        assertFalse(rendered.contains("failedTest11"))
    }

    @Test
    fun `clear resets to empty`() {
        val segment = TestResultsSegment()
        segment.recordResults(5, 1, 0, listOf("x"))
        segment.clear()
        assertTrue(segment.isEmpty())
    }
}
```

- [ ] **Step 11: Write CurrentFocusSegment test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/segments/CurrentFocusSegmentTest.kt
package dev.qyl.shadow.segments

import org.junit.Assert.*
import org.junit.Test

class CurrentFocusSegmentTest {

    @Test
    fun `empty with no open files`() {
        val segment = CurrentFocusSegment()
        assertTrue(segment.isEmpty())
        assertEquals("", segment.render())
    }

    @Test
    fun `renders open files`() {
        val segment = CurrentFocusSegment()
        segment.recordFileOpened("src/Foo.kt")
        segment.recordFileOpened("src/Bar.kt")
        val rendered = segment.render()
        assertTrue(rendered.contains("`src/Foo.kt`"))
        assertTrue(rendered.contains("`src/Bar.kt`"))
    }

    @Test
    fun `renders last edited file`() {
        val segment = CurrentFocusSegment()
        segment.recordFileOpened("src/Foo.kt")
        segment.recordLastEdited("src/Foo.kt")
        val rendered = segment.render()
        assertTrue(rendered.contains("Last edited: `src/Foo.kt`"))
    }

    @Test
    fun `file close removes from open set`() {
        val segment = CurrentFocusSegment()
        segment.recordFileOpened("src/Foo.kt")
        segment.recordFileClosed("src/Foo.kt")
        assertTrue(segment.isEmpty())
    }

    @Test
    fun `clear resets everything`() {
        val segment = CurrentFocusSegment()
        segment.recordFileOpened("src/Foo.kt")
        segment.recordLastEdited("src/Foo.kt")
        segment.clear()
        assertTrue(segment.isEmpty())
    }
}
```

- [ ] **Step 12: Run all segment tests**

```bash
./gradlew test
```

Expected: ALL PASS

- [ ] **Step 13: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/segments/ \
        src/test/kotlin/dev/qyl/shadow/segments/
git commit -m "feat: implement all 7 context segments with tests"
```

---

## Chunk 2: Core Service + Settings + Plugin Wiring

### Task 5: ShadowSettingsState (per-project settings)

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/ShadowSettingsState.kt`

- [ ] **Step 1: Implement settings state**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowSettingsState.kt
package dev.qyl.shadow

import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.Service
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.project.Project

// No @Service annotation — registered via <projectService> in plugin.xml
@State(name = "ShadowContextSettings", storages = [Storage("shadow-context.xml")])
class ShadowSettingsState : PersistentStateComponent<ShadowSettingsState.State> {

    data class State(
        var outputPath: String = ".ide/shadow/context.md",
        var autoGitignore: Boolean = true,
        var maxFileEntries: Int = 20,
        var emitDebounceMs: Long = 2000,
        var sessionTimeoutMinutes: Int = 30,
        var maxManualPins: Int = 10,
        var maxAutoDecisions: Int = 5,
        var enableModifiedFiles: Boolean = true,
        var enableBuildState: Boolean = true,
        var enableDiagnostics: Boolean = true,
        var enableVcsState: Boolean = true,
        var enableTestResults: Boolean = true,
        var enableCurrentFocus: Boolean = true,
        var enableDecisions: Boolean = true,
    )

    private var state = State()

    override fun getState(): State = state

    override fun loadState(state: State) {
        this.state = state
    }

    companion object {
        fun getInstance(project: Project): ShadowSettingsState =
            project.getService(ShadowSettingsState::class.java)
    }
}
```

- [ ] **Step 2: Build**

```bash
./gradlew build
```

Expected: BUILD SUCCESSFUL

- [ ] **Step 3: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/ShadowSettingsState.kt
git commit -m "feat: add ShadowSettingsState per-project PersistentStateComponent"
```

---

### Task 6: ShadowContextService (core emit pipeline + session lifecycle)

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/ShadowContextService.kt`
- Create: `src/test/kotlin/dev/qyl/shadow/ShadowContextRendererTest.kt`

- [ ] **Step 1: Write test**

```kotlin
// src/test/kotlin/dev/qyl/shadow/ShadowContextRendererTest.kt
package dev.qyl.shadow

import dev.qyl.shadow.segments.ContextSegment
import dev.qyl.shadow.segments.Priority
import org.junit.Assert.*
import org.junit.Test
import java.time.Instant

class ShadowContextRendererTest {

    @Test
    fun `renderSnapshot produces markdown with header`() {
        val segments = mapOf(
            "test" to TestSegment("## Test Section\nContent here", Priority.HIGH),
        )
        val rendered = ShadowContextRenderer.render("TestProject", segments)
        assertTrue(rendered.startsWith("# Session Shadow — TestProject"))
        assertTrue(rendered.contains("## Test Section"))
        assertTrue(rendered.contains("Content here"))
    }

    @Test
    fun `empty segments are omitted`() {
        val segments = mapOf(
            "empty" to TestSegment("", Priority.HIGH, empty = true),
            "full" to TestSegment("## Full\nData", Priority.MEDIUM),
        )
        val rendered = ShadowContextRenderer.render("Proj", segments)
        assertFalse(rendered.contains("empty"))
        assertTrue(rendered.contains("## Full"))
    }

    @Test
    fun `segments sorted by priority descending`() {
        val segments = mapOf(
            "low" to TestSegment("## Low", Priority.LOW),
            "critical" to TestSegment("## Critical", Priority.CRITICAL),
            "high" to TestSegment("## High", Priority.HIGH),
        )
        val rendered = ShadowContextRenderer.render("Proj", segments)
        val critIdx = rendered.indexOf("## Critical")
        val highIdx = rendered.indexOf("## High")
        val lowIdx = rendered.indexOf("## Low")
        assertTrue(critIdx < highIdx)
        assertTrue(highIdx < lowIdx)
    }

    private class TestSegment(
        private val content: String,
        override val priority: Priority,
        private val empty: Boolean = false,
    ) : ContextSegment {
        override val key = content.take(10)
        override val lastUpdated: Instant = Instant.now()
        override val enabled = true
        override fun render() = content
        override fun isEmpty() = empty
    }
}
```

- [ ] **Step 2: Create ShadowContextRenderer (pure function, no IDE deps — separate file)**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowContextRenderer.kt
package dev.qyl.shadow

import dev.qyl.shadow.segments.ContextSegment
import java.time.Instant

object ShadowContextRenderer {
    fun render(projectName: String, segments: Map<String, ContextSegment>): String {
        val active = segments.values
            .filter { it.enabled && !it.isEmpty() }
            .sortedByDescending { it.priority }
        return buildString {
            appendLine("# Session Shadow — $projectName")
            appendLine("Generated: ${Instant.now()} | Segments: ${active.size} active")
            appendLine()
            active.forEach { segment ->
                appendLine(segment.render())
                appendLine()
            }
        }.trimEnd()
    }
}
```

- [ ] **Step 3: Create ShadowContextService (with Alarm debounce + session timeout)**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowContextService.kt
package dev.qyl.shadow

import com.intellij.openapi.Disposable
import com.intellij.openapi.project.Project
import com.intellij.util.Alarm
import dev.qyl.shadow.segments.ContextSegment
import java.io.File
import java.time.Instant
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.CopyOnWriteArrayList

class ShadowContextService(private val project: Project) : Disposable {
    private val segments = ConcurrentHashMap<String, ContextSegment>()
    private var lastActivityTimestamp = Instant.now()
    private val emitAlarm = Alarm(Alarm.ThreadToUse.POOLED_THREAD, this)
    private val sessionAlarm = Alarm(Alarm.ThreadToUse.POOLED_THREAD, this)
    private val changeListeners = CopyOnWriteArrayList<() -> Unit>()

    fun addChangeListener(listener: () -> Unit) {
        changeListeners.add(listener)
    }

    fun updateSegment(segment: ContextSegment) {
        lastActivityTimestamp = Instant.now()
        segments[segment.key] = segment
        scheduleEmit()
        resetSessionTimeout()
    }

    fun getSegment(key: String): ContextSegment? = segments[key]

    fun clearAllSegments() {
        segments.clear()
    }

    fun renderCurrent(): String {
        val snapshot = HashMap(segments)
        return ShadowContextRenderer.render(project.name, snapshot)
    }

    // Debounce: cancel-and-reschedule on every call so rapid events
    // coalesce into a single emit after the last event + debounce window.
    private fun scheduleEmit() {
        val debounceMs = ShadowSettingsState.getInstance(project).state.emitDebounceMs
        emitAlarm.cancelAllRequests()
        emitAlarm.addRequest({ emit() }, debounceMs)
    }

    private fun emit() {
        val rendered = renderCurrent()
        writeShadowFile(rendered)
        changeListeners.forEach { it() }
    }

    private fun resetSessionTimeout() {
        sessionAlarm.cancelAllRequests()
        val timeoutMs = ShadowSettingsState.getInstance(project).state
            .sessionTimeoutMinutes * 60L * 1000L
        sessionAlarm.addRequest({ archiveAndReset() }, timeoutMs)
    }

    private fun writeShadowFile(content: String) {
        val basePath = project.basePath ?: return
        val settings = ShadowSettingsState.getInstance(project).state
        val outputFile = File(basePath, settings.outputPath)
        outputFile.parentFile?.mkdirs()
        outputFile.writeText(content)
        if (settings.autoGitignore) ensureGitignore(basePath)
    }

    private fun ensureGitignore(basePath: String) {
        val gitignore = File(basePath, ".gitignore")
        val entry = ".ide/shadow/"
        if (gitignore.exists()) {
            if (!gitignore.readText().contains(entry)) {
                gitignore.appendText("\n# Shadow Context plugin\n$entry\n")
            }
        } else {
            gitignore.writeText("# Shadow Context plugin\n$entry\n")
        }
    }

    fun archiveAndReset() {
        val basePath = project.basePath ?: return
        val settings = ShadowSettingsState.getInstance(project).state
        val outputFile = File(basePath, settings.outputPath)
        if (outputFile.exists()) {
            val archiveFile = File(outputFile.parentFile, "previous.md")
            outputFile.copyTo(archiveFile, overwrite = true)
        }
        clearAllSegments()
        if (outputFile.exists()) outputFile.delete()
        changeListeners.forEach { it() }
    }

    /**
     * Called by ShadowStartupActivity on project open: if the shadow file exists and
     * is younger than the session timeout, skip archive — the session resumes.
     * Otherwise, archive and start fresh.
     */
    fun resumeOrResetSession() {
        val basePath = project.basePath ?: return
        val settings = ShadowSettingsState.getInstance(project).state
        val outputFile = File(basePath, settings.outputPath)
        if (outputFile.exists()) {
            val ageMs = System.currentTimeMillis() - outputFile.lastModified()
            val timeoutMs = settings.sessionTimeoutMinutes * 60L * 1000L
            if (ageMs > timeoutMs) {
                archiveAndReset()
            }
            // else: file is recent — session resumes, segments will be populated by listeners
        }
    }

    override fun dispose() {
        emitAlarm.cancelAllRequests()
        sessionAlarm.cancelAllRequests()
    }

    companion object {
        fun getInstance(project: Project): ShadowContextService =
            project.getService(ShadowContextService::class.java)
    }
}
```

- [ ] **Step 4: Run tests**

```bash
./gradlew test
```

Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/ShadowContextService.kt \
        src/main/kotlin/dev/qyl/shadow/ShadowContextRenderer.kt \
        src/test/kotlin/dev/qyl/shadow/ShadowContextRendererTest.kt
git commit -m "feat: add ShadowContextService with renderer, emit pipeline, session lifecycle"
```

---

### Task 7: plugin.xml + ShadowStartupActivity + ShadowBundle

**Files:**
- Create: `src/main/resources/META-INF/plugin.xml`
- Create: `src/main/kotlin/dev/qyl/shadow/ShadowBundle.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/ShadowStartupActivity.kt`
- Create: `src/main/resources/messages/ShadowBundle.properties`

- [ ] **Step 1: Write plugin.xml**

```xml
<!-- src/main/resources/META-INF/plugin.xml -->
<idea-plugin>
    <id>dev.qyl.shadow</id>
    <name>Shadow Context</name>
    <vendor url="https://github.com/ancplua/shadow-context-plugin">qyl</vendor>

    <description><![CDATA[
    IDE-native context that survives agent memory loss. Works with any AI coding tool.
    Incrementally builds a rolling markdown document from IDE events — file edits, builds,
    diagnostics, VCS, tests — so AI agents can recover context after compaction.
    ]]></description>

    <depends>com.intellij.modules.platform</depends>
    <depends optional="true" config-file="shadow-smrunner.xml">com.intellij.smRunner</depends>
    <depends>com.intellij.modules.vcs</depends>

    <resource-bundle>messages.ShadowBundle</resource-bundle>

    <extensions defaultExtensionNs="com.intellij">
        <backgroundPostStartupActivity
            implementation="dev.qyl.shadow.ShadowStartupActivity"/>

        <checkinHandlerFactory
            implementation="dev.qyl.shadow.ShadowCheckinHandlerFactory"/>

        <projectService
            serviceImplementation="dev.qyl.shadow.ShadowContextService"/>
        <projectService
            serviceImplementation="dev.qyl.shadow.ShadowSettingsState"/>

        <projectConfigurable
            parentId="tools"
            instance="dev.qyl.shadow.ShadowSettingsConfigurable"
            id="dev.qyl.shadow.settings"
            displayName="Shadow Context"/>

        <toolWindow
            id="Shadow Context"
            anchor="bottom"
            factoryClass="dev.qyl.shadow.ShadowToolWindow"
            icon="AllIcons.Actions.Preview"/>
    </extensions>

    <actions>
        <action
            id="dev.qyl.shadow.PinToShadow"
            class="dev.qyl.shadow.PinToShadowAction"
            text="Pin to Shadow"
            description="Pin a decision or note to the shadow context document">
        </action>
    </actions>
</idea-plugin>
```

- [ ] **Step 2: Create smRunner optional config**

```xml
<!-- src/main/resources/META-INF/shadow-smrunner.xml -->
<idea-plugin>
    <!-- Extensions requiring smRunner module are registered here -->
    <!-- TestResultsSegment listener is wired programmatically in IdeEventCollector -->
</idea-plugin>
```

- [ ] **Step 3: Write ShadowBundle**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowBundle.kt
package dev.qyl.shadow

import com.intellij.DynamicBundle
import org.jetbrains.annotations.PropertyKey

private const val BUNDLE = "messages.ShadowBundle"

object ShadowBundle : DynamicBundle(BUNDLE) {
    fun message(@PropertyKey(resourceBundle = BUNDLE) key: String, vararg params: Any): String =
        getMessage(key, *params)
}
```

```properties
# src/main/resources/messages/ShadowBundle.properties
shadow.settings.title=Shadow Context
shadow.settings.output.path=Output path
shadow.settings.auto.gitignore=Auto-add to .gitignore
shadow.settings.max.file.entries=Max file entries
shadow.settings.emit.debounce=Emit debounce (ms)
shadow.settings.session.timeout=Session timeout (minutes)
shadow.settings.max.manual.pins=Max manual pins
shadow.settings.max.auto.decisions=Max auto decisions
shadow.pin.dialog.title=Pin to Shadow
shadow.pin.dialog.prompt=Decision or note:
shadow.toolwindow.copy=Copy to Clipboard
shadow.toolwindow.empty=No shadow context yet. Start coding!
```

- [ ] **Step 4: Write ShadowStartupActivity**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowStartupActivity.kt
package dev.qyl.shadow

import com.intellij.openapi.project.Project
import com.intellij.openapi.startup.ProjectActivity

class ShadowStartupActivity : ProjectActivity {
    override suspend fun execute(project: Project) {
        // Check if existing shadow is recent enough to resume
        ShadowContextService.getInstance(project).resumeOrResetSession()
        // Initialize the event collector — this wires all IDE listeners
        IdeEventCollector(project).subscribe()
    }
}
```

- [ ] **Step 5: Create IdeEventCollector stub (needed for compilation)**

```kotlin
// src/main/kotlin/dev/qyl/shadow/IdeEventCollector.kt
package dev.qyl.shadow

import com.intellij.openapi.project.Project

class IdeEventCollector(private val project: Project) {
    fun subscribe() {
        // Stub — full implementation in Task 8
    }
}
```

- [ ] **Step 6: Build**

```bash
./gradlew build
```

Expected: BUILD SUCCESSFUL

- [ ] **Step 7: Commit**

```bash
git add src/main/resources/ src/main/kotlin/dev/qyl/shadow/ShadowBundle.kt \
        src/main/kotlin/dev/qyl/shadow/ShadowStartupActivity.kt \
        src/main/kotlin/dev/qyl/shadow/IdeEventCollector.kt
git commit -m "feat: add plugin.xml, ShadowBundle, ShadowStartupActivity, IdeEventCollector stub"
```

---

## Chunk 3: IDE Event Wiring + UI

### Task 8: IdeEventCollector (all IDE listeners)

**Files:**
- Modify: `src/main/kotlin/dev/qyl/shadow/IdeEventCollector.kt` (replace stub from Task 7)

- [ ] **Step 1: Replace stub with full IdeEventCollector implementation**

```kotlin
// src/main/kotlin/dev/qyl/shadow/IdeEventCollector.kt
package dev.qyl.shadow

import com.intellij.execution.testframework.sm.runner.SMTRunnerEventsListener
import com.intellij.execution.testframework.sm.runner.SMTestProxy
import com.intellij.openapi.editor.event.DocumentEvent
import com.intellij.openapi.editor.event.DocumentListener
import com.intellij.openapi.editor.EditorFactory
import com.intellij.openapi.fileEditor.FileDocumentManager
import com.intellij.openapi.fileEditor.FileEditorManager
import com.intellij.openapi.fileEditor.FileEditorManagerEvent
import com.intellij.openapi.fileEditor.FileEditorManagerListener
import com.intellij.openapi.project.Project
import com.intellij.openapi.roots.ProjectFileIndex
import com.intellij.openapi.vcs.BranchChangeListener
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.openapi.vfs.VirtualFileManager
import com.intellij.openapi.vfs.newvfs.BulkFileListener
import com.intellij.openapi.vfs.newvfs.events.VFileCreateEvent
import com.intellij.openapi.vfs.newvfs.events.VFileDeleteEvent
import com.intellij.openapi.vfs.newvfs.events.VFileEvent
import com.intellij.problems.ProblemListener
import com.intellij.refactoring.listeners.RefactoringEventData
import com.intellij.refactoring.listeners.RefactoringEventListener
import com.intellij.task.ProjectTaskListener
import com.intellij.task.ProjectTaskManager
import dev.qyl.shadow.segments.*

class IdeEventCollector(private val project: Project) {

    fun subscribe() {
        val service = ShadowContextService.getInstance(project)
        val settings = ShadowSettingsState.getInstance(project).state
        val connection = project.messageBus.connect(service)

        // --- Decisions segment (shared, used by multiple listeners) ---
        val decisions = DecisionsSegment(
            maxManualPins = settings.maxManualPins,
            maxAutoDecisions = settings.maxAutoDecisions,
        )
        decisions.enabled = settings.enableDecisions
        service.updateSegment(decisions)

        // --- File edits ---
        val modifiedFiles = ModifiedFilesSegment(maxEntries = settings.maxFileEntries)
        modifiedFiles.enabled = settings.enableModifiedFiles
        service.updateSegment(modifiedFiles)

        EditorFactory.getInstance().eventMulticaster.addDocumentListener(object : DocumentListener {
            override fun documentChanged(event: DocumentEvent) {
                val file = FileDocumentManager.getInstance().getFile(event.document) ?: return
                if (!isInProject(file)) return
                val path = relativePath(file) ?: return
                modifiedFiles.recordEdit(path, isNew = false)
                service.updateSegment(modifiedFiles)
            }
        }, service)

        // --- File creation/deletion → auto-decisions ---
        connection.subscribe(VirtualFileManager.VFS_CHANGES, object : BulkFileListener {
            override fun after(events: List<VFileEvent>) {
                for (event in events) {
                    val file = event.file ?: continue
                    if (!isInProject(file)) continue
                    val path = relativePath(file) ?: continue
                    when (event) {
                        is VFileCreateEvent -> {
                            modifiedFiles.recordEdit(path, isNew = true)
                            decisions.addAutoDecision("created $path")
                        }
                        is VFileDeleteEvent -> {
                            modifiedFiles.recordDeletion(path)
                            decisions.addAutoDecision("deleted $path")
                        }
                    }
                }
                service.updateSegment(modifiedFiles)
                service.updateSegment(decisions)
            }
        })

        // --- Build results (platform-level, works in all IDEs) ---
        val buildState = BuildStateSegment()
        buildState.enabled = settings.enableBuildState
        connection.subscribe(ProjectTaskListener.TOPIC, object : ProjectTaskListener {
            override fun finished(result: ProjectTaskManager.Result) {
                buildState.recordBuild(
                    success = !result.isAborted && !result.hasErrors(),
                    errors = emptyList(), // Error details not available from Result
                    warningCount = 0,
                )
                service.updateSegment(buildState)
            }
        })

        // --- Diagnostics (platform-level WolfTheProblemSolver) ---
        val diagnostics = DiagnosticsSegment()
        diagnostics.enabled = settings.enableDiagnostics
        connection.subscribe(ProblemListener.TOPIC, object : ProblemListener {
            override fun problemsAppeared(file: VirtualFile) {
                if (!isInProject(file)) return
                updateDiagnostics(diagnostics, service)
            }
            override fun problemsDisappeared(file: VirtualFile) {
                if (!isInProject(file)) return
                updateDiagnostics(diagnostics, service)
            }
        })
        service.updateSegment(diagnostics)

        // --- VCS + auto-decisions for branch switch ---
        val vcsState = VcsStateSegment()
        vcsState.enabled = settings.enableVcsState
        connection.subscribe(BranchChangeListener.VCS_BRANCH_CHANGED, object : BranchChangeListener {
            override fun branchWillChange(branchName: String) {}
            override fun branchHasChanged(branchName: String) {
                vcsState.updateBranch(branchName)
                decisions.addAutoDecision("switched to $branchName")
                service.updateSegment(vcsState)
                service.updateSegment(decisions)
            }
        })

        // --- Rename refactoring auto-decisions (platform-level) ---
        connection.subscribe(RefactoringEventListener.REFACTORING_EVENT_TOPIC, object : RefactoringEventListener {
            override fun refactoringDone(refactoringId: String, afterData: RefactoringEventData?) {
                if (refactoringId == "refactoring.rename") {
                    val name = afterData?.getStringProperties()?.firstOrNull()
                        ?: afterData?.getElement()?.toString()
                        ?: "unknown"
                    decisions.addAutoDecision("renamed → $name")
                    service.updateSegment(decisions)
                }
            }
        })

        // --- File focus ---
        val focusSegment = CurrentFocusSegment()
        focusSegment.enabled = settings.enableCurrentFocus
        connection.subscribe(FileEditorManagerListener.FILE_EDITOR_MANAGER, object : FileEditorManagerListener {
            override fun fileOpened(source: FileEditorManager, file: VirtualFile) {
                val path = relativePath(file) ?: return
                focusSegment.recordFileOpened(path)
                service.updateSegment(focusSegment)
            }
            override fun fileClosed(source: FileEditorManager, file: VirtualFile) {
                val path = relativePath(file) ?: return
                focusSegment.recordFileClosed(path)
                service.updateSegment(focusSegment)
            }
            override fun selectionChanged(event: FileEditorManagerEvent) {
                val file = event.newFile ?: return
                val path = relativePath(file) ?: return
                focusSegment.recordLastEdited(path)
                service.updateSegment(focusSegment)
            }
        })

        // --- Test results (optional smRunner) ---
        trySubscribeTests(connection, service, settings)
    }

    private fun updateDiagnostics(segment: DiagnosticsSegment, service: ShadowContextService) {
        // WolfTheProblemSolver tracks which files have problems.
        // For v0.1, we detect that problems changed and mark the segment updated.
        // A future version can read specific diagnostics from WolfTheProblemSolver.
        val wolf = com.intellij.problems.WolfTheProblemSolver.getInstance(project)
        val modifiedFiles = service.getSegment("modified-files") as? ModifiedFilesSegment
        val diagnosticEntries = mutableListOf<DiagnosticEntry>()
        modifiedFiles?.getFilePaths()?.forEach { path ->
            val vf = VirtualFileManager.getInstance().findFileByUrl("file://${project.basePath}/$path")
            if (vf != null && wolf.isProblemFile(vf)) {
                diagnosticEntries.add(DiagnosticEntry(path, "ERROR", "Has problems (see IDE)"))
            }
        }
        segment.update(diagnosticEntries)
        service.updateSegment(segment)
    }

    private fun trySubscribeTests(
        connection: com.intellij.util.messages.MessageBusConnection,
        service: ShadowContextService,
        settings: ShadowSettingsState.State,
    ) {
        if (!settings.enableTestResults) return
        try {
            val testResults = TestResultsSegment()
            connection.subscribe(SMTRunnerEventsListener.TEST_STATUS, object : SMTRunnerEventsListener {
                override fun onTestingFinished(testsRoot: SMTestProxy.SMRootTestProxy) {
                    val all = testsRoot.allTests
                    val passed = all.count { it.isPassed }
                    val failed = all.count { it.isDefect }
                    val skipped = all.size - passed - failed
                    val failedNames = all.filter { it.isDefect }.take(10).map { it.presentableName }
                    testResults.recordResults(passed, failed, skipped, failedNames)
                    service.updateSegment(testResults)
                }
            })
        } catch (_: Throwable) {
            // smRunner not available — NoClassDefFoundError extends Error, not Exception
        }
    }

    private fun isInProject(file: VirtualFile): Boolean =
        ProjectFileIndex.getInstance(project).isInContent(file)

    private fun relativePath(file: VirtualFile): String? {
        val basePath = project.basePath ?: return null
        val filePath = file.path
        return if (filePath.startsWith(basePath)) {
            filePath.removePrefix(basePath).removePrefix("/")
        } else null
    }
}
```

- [ ] **Step 2: Build**

```bash
./gradlew build
```

Expected: BUILD SUCCESSFUL (some API references may need adjustment based on exact SDK version — fix compilation errors if any)

- [ ] **Step 3: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/IdeEventCollector.kt
git commit -m "feat: add IdeEventCollector wiring all IDE event listeners to segments"
```

---

### Task 9: PinToShadowAction

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/PinToShadowAction.kt`

- [ ] **Step 1: Implement action**

```kotlin
// src/main/kotlin/dev/qyl/shadow/PinToShadowAction.kt
package dev.qyl.shadow

import com.intellij.openapi.actionSystem.AnAction
import com.intellij.openapi.actionSystem.AnActionEvent
import com.intellij.openapi.ui.Messages
import dev.qyl.shadow.segments.DecisionsSegment

class PinToShadowAction : AnAction() {
    override fun actionPerformed(e: AnActionEvent) {
        val project = e.project ?: return
        val input = Messages.showInputDialog(
            project,
            ShadowBundle.message("shadow.pin.dialog.prompt"),
            ShadowBundle.message("shadow.pin.dialog.title"),
            null,
        )
        if (input.isNullOrBlank()) return

        val service = ShadowContextService.getInstance(project)
        val decisions = service.getSegment("decisions") as? DecisionsSegment ?: return
        decisions.addManualPin(input)
        service.updateSegment(decisions)
    }

    override fun update(e: AnActionEvent) {
        e.presentation.isEnabledAndVisible = e.project != null
    }
}
```

- [ ] **Step 2: Build**

```bash
./gradlew build
```

- [ ] **Step 3: Implement ShadowCheckinHandlerFactory (git commit auto-decisions)**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowCheckinHandlerFactory.kt
package dev.qyl.shadow

import com.intellij.openapi.vcs.CheckinProjectPanel
import com.intellij.openapi.vcs.changes.CommitContext
import com.intellij.openapi.vcs.checkin.CheckinHandler
import com.intellij.openapi.vcs.checkin.CheckinHandlerFactory
import dev.qyl.shadow.segments.DecisionsSegment
import dev.qyl.shadow.segments.VcsStateSegment

class ShadowCheckinHandlerFactory : CheckinHandlerFactory() {
    override fun createHandler(panel: CheckinProjectPanel, commitContext: CommitContext): CheckinHandler {
        return object : CheckinHandler() {
            override fun checkinSuccessful() {
                val project = panel.project
                val service = ShadowContextService.getInstance(project)
                val commitMessage = panel.commitMessage.lines().firstOrNull()?.take(80) ?: "unknown"

                // Update VCS state with commit message
                val vcs = service.getSegment("vcs-state") as? VcsStateSegment
                vcs?.updateLastCommit(commitMessage)
                if (vcs != null) service.updateSegment(vcs)

                // Add auto-decision
                val decisions = service.getSegment("decisions") as? DecisionsSegment
                decisions?.addAutoDecision("committed: $commitMessage")
                if (decisions != null) service.updateSegment(decisions)
            }
        }
    }
}
```

- [ ] **Step 4: Build**

```bash
./gradlew build
```

- [ ] **Step 5: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/PinToShadowAction.kt \
        src/main/kotlin/dev/qyl/shadow/ShadowCheckinHandlerFactory.kt
git commit -m "feat: add PinToShadowAction and ShadowCheckinHandlerFactory"
```

---

### Task 10: ShadowToolWindow + ShadowSettingsConfigurable

**Files:**
- Create: `src/main/kotlin/dev/qyl/shadow/ShadowToolWindow.kt`
- Create: `src/main/kotlin/dev/qyl/shadow/ShadowSettingsConfigurable.kt`

- [ ] **Step 1: Implement tool window**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowToolWindow.kt
package dev.qyl.shadow

import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.openapi.wm.ToolWindow
import com.intellij.openapi.wm.ToolWindowFactory
import com.intellij.ui.components.JBScrollPane
import com.intellij.ui.content.ContentFactory
import java.awt.BorderLayout
import java.awt.datatransfer.StringSelection
import java.awt.Toolkit
import javax.swing.JButton
import javax.swing.JPanel
import javax.swing.JTextArea
import javax.swing.SwingUtilities

class ShadowToolWindow : ToolWindowFactory, DumbAware {
    override fun createToolWindowContent(project: Project, toolWindow: ToolWindow) {
        val service = ShadowContextService.getInstance(project)
        val textArea = JTextArea().apply {
            isEditable = false
            font = java.awt.Font("JetBrains Mono", java.awt.Font.PLAIN, 12)
        }

        val copyButton = JButton(ShadowBundle.message("shadow.toolwindow.copy")).apply {
            addActionListener {
                val content = textArea.text
                if (content.isNotBlank()) {
                    val clipboard = Toolkit.getDefaultToolkit().systemClipboard
                    clipboard.setContents(StringSelection(content), null)
                }
            }
        }

        val panel = JPanel(BorderLayout()).apply {
            add(JBScrollPane(textArea), BorderLayout.CENTER)
            add(copyButton, BorderLayout.SOUTH)
        }

        // Event-driven: re-render when any segment changes (no polling)
        fun refresh() {
            val rendered = service.renderCurrent()
            if (rendered != textArea.text) {
                textArea.text = rendered.ifBlank { ShadowBundle.message("shadow.toolwindow.empty") }
                textArea.caretPosition = 0
            }
        }

        service.addChangeListener {
            SwingUtilities.invokeLater { refresh() }
        }

        // Initial render
        refresh()

        val content = ContentFactory.getInstance().createContent(panel, "", false)
        toolWindow.contentManager.addContent(content)
    }
}
```

- [ ] **Step 2: Implement settings configurable**

```kotlin
// src/main/kotlin/dev/qyl/shadow/ShadowSettingsConfigurable.kt
package dev.qyl.shadow

import com.intellij.openapi.options.Configurable
import com.intellij.openapi.project.Project
import javax.swing.*

class ShadowSettingsConfigurable(private val project: Project) : Configurable {
    private var outputPathField: JTextField? = null
    private var autoGitignoreCheck: JCheckBox? = null
    private var maxFileEntriesSpinner: JSpinner? = null
    private var emitDebounceSpinner: JSpinner? = null
    private var sessionTimeoutSpinner: JSpinner? = null
    private var maxManualPinsSpinner: JSpinner? = null
    private var maxAutoDecisionsSpinner: JSpinner? = null

    override fun getDisplayName(): String = ShadowBundle.message("shadow.settings.title")

    override fun createComponent(): JComponent {
        val settings = ShadowSettingsState.getInstance(project).state
        val panel = JPanel().apply { layout = BoxLayout(this, BoxLayout.Y_AXIS) }

        outputPathField = JTextField(settings.outputPath, 30)
        autoGitignoreCheck = JCheckBox(ShadowBundle.message("shadow.settings.auto.gitignore"), settings.autoGitignore)
        maxFileEntriesSpinner = JSpinner(SpinnerNumberModel(settings.maxFileEntries, 1, 100, 1))
        emitDebounceSpinner = JSpinner(SpinnerNumberModel(settings.emitDebounceMs.toInt(), 500, 10000, 500))
        sessionTimeoutSpinner = JSpinner(SpinnerNumberModel(settings.sessionTimeoutMinutes, 5, 480, 5))
        maxManualPinsSpinner = JSpinner(SpinnerNumberModel(settings.maxManualPins, 1, 50, 1))
        maxAutoDecisionsSpinner = JSpinner(SpinnerNumberModel(settings.maxAutoDecisions, 1, 20, 1))

        fun row(label: String, component: JComponent): JPanel =
            JPanel(java.awt.FlowLayout(java.awt.FlowLayout.LEFT)).apply {
                add(JLabel(label))
                add(component)
            }

        panel.add(row(ShadowBundle.message("shadow.settings.output.path"), outputPathField!!))
        panel.add(autoGitignoreCheck)
        panel.add(row(ShadowBundle.message("shadow.settings.max.file.entries"), maxFileEntriesSpinner!!))
        panel.add(row(ShadowBundle.message("shadow.settings.emit.debounce"), emitDebounceSpinner!!))
        panel.add(row(ShadowBundle.message("shadow.settings.session.timeout"), sessionTimeoutSpinner!!))
        panel.add(row(ShadowBundle.message("shadow.settings.max.manual.pins"), maxManualPinsSpinner!!))
        panel.add(row(ShadowBundle.message("shadow.settings.max.auto.decisions"), maxAutoDecisionsSpinner!!))

        return panel
    }

    override fun isModified(): Boolean {
        val s = ShadowSettingsState.getInstance(project).state
        return outputPathField?.text != s.outputPath ||
            autoGitignoreCheck?.isSelected != s.autoGitignore ||
            (maxFileEntriesSpinner?.value as? Int) != s.maxFileEntries ||
            (emitDebounceSpinner?.value as? Int)?.toLong() != s.emitDebounceMs ||
            (sessionTimeoutSpinner?.value as? Int) != s.sessionTimeoutMinutes ||
            (maxManualPinsSpinner?.value as? Int) != s.maxManualPins ||
            (maxAutoDecisionsSpinner?.value as? Int) != s.maxAutoDecisions
    }

    override fun apply() {
        val s = ShadowSettingsState.getInstance(project).state
        s.outputPath = outputPathField?.text ?: s.outputPath
        s.autoGitignore = autoGitignoreCheck?.isSelected ?: s.autoGitignore
        s.maxFileEntries = (maxFileEntriesSpinner?.value as? Int) ?: s.maxFileEntries
        s.emitDebounceMs = (emitDebounceSpinner?.value as? Int)?.toLong() ?: s.emitDebounceMs
        s.sessionTimeoutMinutes = (sessionTimeoutSpinner?.value as? Int) ?: s.sessionTimeoutMinutes
        s.maxManualPins = (maxManualPinsSpinner?.value as? Int) ?: s.maxManualPins
        s.maxAutoDecisions = (maxAutoDecisionsSpinner?.value as? Int) ?: s.maxAutoDecisions
    }

    override fun reset() {
        val s = ShadowSettingsState.getInstance(project).state
        outputPathField?.text = s.outputPath
        autoGitignoreCheck?.isSelected = s.autoGitignore
        maxFileEntriesSpinner?.value = s.maxFileEntries
        emitDebounceSpinner?.value = s.emitDebounceMs.toInt()
        sessionTimeoutSpinner?.value = s.sessionTimeoutMinutes
        maxManualPinsSpinner?.value = s.maxManualPins
        maxAutoDecisionsSpinner?.value = s.maxAutoDecisions
    }
}
```

- [ ] **Step 3: Build**

```bash
./gradlew build
```

- [ ] **Step 4: Commit**

```bash
git add src/main/kotlin/dev/qyl/shadow/ShadowToolWindow.kt \
        src/main/kotlin/dev/qyl/shadow/ShadowSettingsConfigurable.kt
git commit -m "feat: add ShadowToolWindow preview and ShadowSettingsConfigurable"
```

---

## Chunk 4: Integration Test + Ship

### Task 11: Full integration build + runIde smoke test

- [ ] **Step 1: Run full build with verification**

```bash
./gradlew clean build
```

Expected: BUILD SUCCESSFUL, all tests pass

- [ ] **Step 2: Run plugin verifier**

```bash
./gradlew verifyPlugin
```

Expected: No compatibility issues

- [ ] **Step 3: Run IDE with plugin loaded**

```bash
./gradlew runIde
```

Expected: Rider/IntelliJ IDEA opens with Shadow Context plugin active. Verify:
1. `Settings → Tools → Shadow Context` shows settings panel
2. `View → Tool Windows → Shadow Context` shows preview panel
3. Edit a file → shadow context updates
4. Pin to Shadow action works (find in `Actions` menu)

- [ ] **Step 4: Verify output file**

Open a project in the test IDE, edit a file, check that `.ide/shadow/context.md` was created with correct content.

- [ ] **Step 5: Commit final state**

```bash
git add -A
git commit -m "chore: integration verified — Shadow Context plugin v0.1.0"
```

---

### Task 12: Marketplace preparation

- [ ] **Step 1: Update README with plugin description markers**

The `build.gradle.kts` extracts description from README between markers:

```markdown
<!-- Plugin description -->
Shadow Context generates a rolling markdown document from IDE events — file edits, builds,
diagnostics, VCS operations, and test results. Any AI coding agent can read this document
to recover context after memory compaction. Agent-agnostic. Zero configuration required.
<!-- Plugin description end -->
```

- [ ] **Step 2: Create initial CHANGELOG**

```markdown
# Changelog

## [0.1.0]
### Added
- Incremental shadow context generation from IDE events
- 7 segment types: Modified Files, Build State, Diagnostics, VCS, Tests, Focus, Decisions
- Pin to Shadow action for manual decision annotation
- Auto-detected decisions (commits, renames, file creation)
- Shadow Preview tool window with clipboard copy
- Per-project configurable settings
- Auto-gitignore for shadow output directory
- Session lifecycle with timeout and archive
- Optional smRunner dependency for test results
```

- [ ] **Step 3: Build plugin zip**

```bash
./gradlew buildPlugin
```

Expected: `build/distributions/Shadow Context-0.1.0.zip` created

- [ ] **Step 4: Commit**

```bash
git add README.md CHANGELOG.md
git commit -m "docs: add plugin description and changelog for v0.1.0"
```
