// Copyright (c) 2025-2026 ancplua

namespace Qyl.Loom.Autofix.Workflow;

/// Per-trigger workflow-config registry. Callers (dashboard /code-it-up,
/// triage auto-route, future REST API) Set a config keyed by runId before
/// AutofixAgentService picks the run up. The runner consults the store; if
/// nothing is registered it falls back to AutofixWorkflowDefaults.Autonomous.
internal sealed class AutofixRunConfigStore
{
    private readonly ConcurrentDictionary<string, AutofixWorkflowConfig> _configs =
        new(StringComparer.Ordinal);

    public void Set(string runId, AutofixWorkflowConfig config) => _configs[runId] = config;

    public bool TryGet(string runId, out AutofixWorkflowConfig config) =>
        _configs.TryGetValue(runId, out config!);

    public bool TryRemove(string runId) => _configs.TryRemove(runId, out _);
}
