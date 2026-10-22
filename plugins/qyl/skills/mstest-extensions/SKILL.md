---
name: mstest-extensions
description: >-
  Design, explain, implement or troubleshoot reusable native MSTest extensions:
  custom data and assertions, execution attributes, policies, fixtures and
  programmatic filters. Includes extension API questions and planning without
  a consumer project. Ordinary tests, built-in API questions and project setup
  belong to mstest-writing-tests; test commands belong to mstest-run-tests.
  Report-only test-quality reviews and Native AOT have focused sibling skills.
---

# MSTest testing and extensions

Use the opinionated native stack: MSTest.Sdk 4.5.1,
MSTest 4.5.1, MTP 2.5.1, `AllMicrosoft` and Microsoft.CodeCoverage. Let the SDK
configure its runner and extensions. Use native context and lifecycle APIs rather
than implementing copies or older-version fallback branches.

Build the requested capability with the consuming project's resolved MSTest APIs. Start
with built-in behavior; create an extension when it removes repeated complexity
or expresses a domain contract.

Identify the test's boundary before choosing fixtures or verification. Unit,
integration, end-to-end and shared-helper projects have different resource and
delivery contracts; their names are hints rather than proof.

## Establish the API and execution contract

For conceptual planning without a consumer project or selected versions, explain
the mechanisms and contracts that need
version-specific verification. State what remains unresolved; do not create a
project or restore packages just to answer a conceptual question. Give verified
signatures or executable claims only when the required evidence exists.

For implementation or claims about a specific consumer, read its instructions,
package/SDK pins, target frameworks,
`global.json`, and existing tests. Identify MSTest.TestFramework, the adapter,
MSTest.Sdk if used, and confirm native Microsoft.Testing.Platform (MTP).
Report a different runner without adding a bridge or silently migrating it.
Preserve native package versions and the coverage provider unless changing them
is part of the task. Resolve the actual restored versions
for each target/configuration at the start of a task; a repository name or an
earlier conversation's version is not current evidence.

Use the bundled
[API inspector](scripts/inspect_mstest.py) to identify evaluated package requests,
resolved framework/adapter/platform versions, SDK, source commits, and assembly
fingerprints. Changed versions, assets, targets, runner settings, or generation
mode invalidate earlier API evidence. Resolve relevant signatures from the
installed package and its matching source before reusing an earlier pattern.
The skill supports this lookup for unlisted versions; a release note entry is not
a prerequisite.

Name the observable behavior, its public entrypoint, and the owner of each
resource. Keep explanations and reviews read-only. For implementation, choose a
case that distinguishes the required behavior from a plausible incorrect result.

To resolve the needed API, prefer a
user-supplied matching solution and installed package metadata; distinguish a
consumer sample from framework implementation. Check official version-matched
source when signatures or runtime behavior remain unclear. Current documentation
can include later or preview APIs. Compile before calling a code example verified.

## Choose the extension point

| Need | MSTest mechanism |
| --- | --- |
| Inline or computed cases | `[TestMethod]` with `[DataRow]` or `[DynamicData]` |
| Reusable named scenario attribute | `Attribute, ITestDataSource` |
| Independent input axes or custom parameter values | `[CombinatorialData]`, `ICombinatorialValuesProvider` when available |
| A repeated domain expectation | Extension method on `Assert`, called through `Assert.That` |
| A policy around a test invocation | `TestMethodAttribute.ExecuteAsync`; class-wide composition through `TestClassAttribute` |
| Conditional execution or retry policy | Built-in conditions/retries, then `ConditionBaseAttribute` or `RetryBaseAttribute` |
| Per-test resources, shared setup, or controlled suspension | Initialization/cleanup methods, `TestContext`, explicit resource ownership |
| Coordinating shared resources under parallel execution | Explicit parallel policy and `[ResourceLock]` when available |
| Constructing test classes from an application-owned service provider | Installed hosting integration and its compatibility limits |
| Programmatic run selection | `ITestFilter` and one assembly-level provider |
| Discovery, reporting, MTP integration, or source generation/AOT | Existing runner and its supported extension APIs |
| Review test quality, strengthen assertions, or establish what a change's tests prove | Scope the review and trace observable outcomes |
| Verify telemetry capture, privacy, persistence, or native span ownership | Separate producer, transport, storage and read-time contracts |

For a focused correction, fix the demonstrated issue; do not turn one assertion
edit into a suite-wide audit. A review of changed tests distinguishes code
findings, possible gaps and executed proof.

## Implement with MSTest semantics

Keep data descriptions immutable and create mutable resources per execution.
Discovery and data enumeration must not acquire per-invocation resources.
Optional hosting can bootstrap a root host even for discovery; keep its startup
bounded and verify it creates no invocation scopes. Preserve every result,
original failure, data-row argument and lifecycle behavior when wrapping execution.
Let MSTest own row expansion; do not manually re-enumerate `[DataRow]` inside a
wrapper or collapse its results.
A required verification or cleanup failure must remain visible, including when
another test failure already exists. Optional diagnostic capture has a different
failure policy.

Use published framework or domain constants for fixed contract strings. Define
shared named constants for owned keys, event names, configuration settings,
resource identifiers and diagnostic markers instead of repeating literals. Keep
typed enum selectors typed: an enum member is not a string constant. Pass it directly to a typed attribute constructor; do not replace
it with a string or `.ToString()`. Use `nameof` for member/type references. Keep distinct adversarial inputs distinct;
do not normalize test values or create a second catalog of published constants.

Scope experimental API acknowledgments to the specific code that requires them.
Where supported, declare an internal wrapper's experimental dependency with the
same diagnostic ID instead of suppressing the diagnostic. Do not disable
analyzers project-wide. Native invocation context is version-dependent.
For class-created execution wrappers, source navigation comes from discovery of
the original test attribute; resolve it before copying caller metadata or adding
an inspection suppression.

An `ITestDataSource` is a runtime data provider. MSTest source generation is a
separate runner/discovery feature. An `Assert.That` extension is an ordinary C#
extension method; it does not need an assertion generator. Synchronous MSTest
assertions throw immediately; await operations and async assertions that return
tasks. Do not transfer another framework's fixture injection, executor, or
parallel-default assumptions into MSTest.

## Verify the capability

Discover the affected tests through the consuming project's normal entrypoint;
check row names and counts, then run the focused cases. Use the normal test
entrypoint for the first compilation of changed code, allowing its usual restore
for newly added projects. A separate build is warranted only when it establishes
something the test command cannot, such as a required published artifact.
Resolve assertion overloads and enabled analyzer recommendations before writing
tests. Reuse passing results while
their relevant source, fixtures, packages
and configuration are unchanged. Prose-only guidance needs content and package
checks, not another test run. For a custom assertion, exercise acceptance and rejection. For data
providers, verify repeatable enumeration and meaningful combinations. For
execution policies, check a real runner failure as well as a passing invocation;
an exception swallowed into a passing result is a defect.

For resources and async work, verify per-test isolation, actual suspension,
successful resumption, cancellation, and joining before disposal, including when
observation and cleanup can both fail. Use a separate
process for deliberately failing runner probes, and check its exit status and
structured results rather than treating a nonzero exit alone as success.

Report the commands, discovered/executed cases, and remaining limits. Keep model
content evaluations, automatic skill selection, compilation, runner execution,
and Native AOT publication as separate evidence. Repository-specific commands and
test baselines belong in the project instructions, outside this portable skill.

For executable checks of retry/filter outcomes, optional host scope disposal, or
published source-generated and Native AOT consumers, use the packaged
[capability verifier](scripts/verify_capabilities.py). It runs isolated
fixtures with explicit versions; it does not change the consumer's packages or
establish compatibility for untested extension shapes.
