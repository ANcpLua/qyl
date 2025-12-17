---
name: test-timeout-reminder
enabled: true
event: bash
pattern: dotnet\s+test|npm\s+test|pytest|jest|vitest|cargo\s+test
---

**Test Suite Timeout Reminder**

The entire test suite should **never run more than 5 minutes**.

If tests are taking longer:
- Check for slow integration tests
- Look for missing test parallelization
- Consider if external dependencies are causing delays
- Review test setup/teardown efficiency

Use `--timeout` flags or equivalent if available to enforce this limit.
