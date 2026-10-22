---
name: qyl-development
description: Connect source analysis, native MSTest verification and qyl telemetry when a .NET investigation or fix spans those boundaries. Use for end-to-end debugging with code and runtime evidence; focused build, test or telemetry requests use the corresponding bundled skill directly.
---

# Investigate and improve .NET software

Start from the user's symptom and available evidence. Choose only the relevant
parts below; an ordinary code edit does not require production telemetry.

| Work | Bundled guidance |
| --- | --- |
| Recorded failures, traces, logs, metrics or whether a deployed fix held | [qyl-investigate](../qyl-investigate/SKILL.md) |
| Runtime cost and profiling | [performance](../performance/SKILL.md) |
| Build, package versions or analyzers | [audit](../audit/SKILL.md), then its focused skill |
| Structural code changes, constants or abandoned paths | [csharp-refactoring](../csharp-refactoring/SKILL.md) |
| Tests, extensions, execution or test-quality evidence | [mstest-writing-tests](../mstest-writing-tests/SKILL.md), [mstest-run-tests](../mstest-run-tests/SKILL.md), [mstest-extensions](../mstest-extensions/SKILL.md) |

Connect runtime observations to the matching source revision and configuration.
Keep observed facts, source hypotheses and missing evidence distinct. Fix the
responsible code and use the project's relevant existing verification. For a
deployed change, compare equivalent workloads and state whether enough evidence
exists to assess it.

The hosted qyl tools access only the caller's authorized telemetry. Repository
edits, builds and native tests run through the agent's available workspace tools;
the plugin does not provide remote build or test execution. If a required resource
is unavailable, report that boundary and continue the parts the user authorized.
