
namespace Qyl.Loom.Autofix.Workflow;

internal sealed class AutofixRunConfigStore
{
    private readonly ConcurrentDictionary<string, AutofixWorkflowConfig> _configs =
        new(StringComparer.Ordinal);

    public void Set(string runId, AutofixWorkflowConfig config) => _configs[runId] = config;

    public bool TryGet(string runId, out AutofixWorkflowConfig config) =>
        _configs.TryGetValue(runId, out config!);

    public bool TryRemove(string runId) => _configs.TryRemove(runId, out _);
}
