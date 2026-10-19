# Bundled MSTest routing

All skills below are included in qyl. Select the smallest matching skill; preserve the caller's scope, including read-only audits.

| Task | Bundled skill |
| --- | --- |
| Test/project edits, built-in APIs, lifecycle, solution or CI inclusion | `qyl:mstest-writing-tests` |
| Commands, runner identification, discovery, filtering, TRX, coverage, zero tests | `qyl:mstest-run-tests` |
| Reusable data/assertions, execution attributes, policies, fixtures, custom filters | `qyl:mstest-extensions` |
| Practical report-only quality, flakiness and maintainability review | `qyl:mstest-test-review` |
| Assertion-only report | `qyl:mstest-assertion-review` |
| Formal named test-smell taxonomy | `qyl:mstest-test-smells` |
| Hypothetical behavior changes the suite would miss | `qyl:mstest-test-gaps` |
| Evidence for an identified fix, feature or change | `qyl:mstest-test-value` |
| Native AOT publication, trimming and generated discovery | `qyl:mstest-native-aot` |


Use the bundled .NET skills for general MSBuild, package versions and analyzers. Use these MSTest skills for test semantics. Do not preload every skill.

If an entry is missing, report an incomplete qyl installation; do not claim it ran or install a duplicate companion plugin.
