---
name: performance
description: Investigate .NET runtime performance using logs, metrics, traces, spans and profiles; collect crash/hang diagnostics. Use for CPU, GC, allocation or latency problems, hot-path reviews and BenchmarkDotNet comparisons. MSBuild timing belongs to msbuild; MSTest execution belongs to mstest-extensions.
license: Apache-2.0
---

# .NET runtime performance

## Process

1. **Name the question.** Establish the workload, symptom, runtime, OS and comparison:
   implementations, revisions, runtimes or input sizes. Keep source-review
   hypotheses separate from measured costs. A command-only request needs commands,
   not an unrequested collection session.
2. **Choose the evidence.** Pick the matching row below. Reuse the
   repository's telemetry, tools and package policy. Correlate deployed revision,
   workload, time window and trace/span identifiers before collecting more data.
   Bound collection duration and benchmark
   case count to the question; keep investigative artifacts outside tracked source.
3. **Compare like with like.** Hold workload and environment constant. Check result
   equivalence before interpreting speedups. Report the measured scope, uncertainty
   and artifact paths; an inconclusive result is not an optimization claim.

| Question | Evidence |
| --- | --- |
| Which implementation, runtime or revision is faster? | Benchmarks |
| Which production operation is slow, and where does its time go? | Logs, metrics, traces/spans, then targeted profiles |
| Where does CPU, memory or waiting go; how do I capture a crash/hang? | Profiles and crash/hang dumps |
| Which source patterns warrant a measured change? | Hot-path review, then a measurement |

Build-time work uses [msbuild](../msbuild/SKILL.md). This skill owns measurement,
not another set of test instructions.
