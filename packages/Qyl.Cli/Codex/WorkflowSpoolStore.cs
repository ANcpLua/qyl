namespace Qyl.Cli.Codex;

internal sealed class WorkflowSpoolStore
{
    private readonly WorkflowSpoolProtector _protector;

    public WorkflowSpoolStore(string root)
    {
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
        _protector = WorkflowSpoolProtector.Open(Root);
    }

    public string Root { get; }

    public WorkflowSpool Open(string runId) => new(Root, runId, _protector);

    public IReadOnlyList<WorkflowSpool> Enumerate()
    {
        var runs = Path.Combine(Root, "runs");
        if (!Directory.Exists(runs))
            return [];
        return Directory.EnumerateDirectories(runs)
            .Order(StringComparer.Ordinal)
            .Select(path => Open(Path.GetFileName(path)))
            .ToArray();
    }
}
