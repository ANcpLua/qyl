namespace Qyl.Orchestration.Internal;

internal static class QylResourceGraph
{
    internal static void Validate(IReadOnlyList<QylResource> resources)
    {
        var byName = resources.ToDictionary(static r => r.Name, StringComparer.Ordinal);

        foreach (var resource in resources)
            foreach (var dep in resource.WaitsFor.Where(dep => !byName.ContainsKey(dep)))
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' waits for unknown resource '{dep}'.");
            }

        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string name)
        {
            if (done.Contains(name))
                return;
            if (!visiting.Add(name))
            {
                throw new InvalidOperationException(
                    $"WaitFor cycle detected involving resource '{name}' — a cycle can never become ready.");
            }

            foreach (var dep in byName[name].WaitsFor)
                Visit(dep);
            visiting.Remove(name);
            done.Add(name);
        }

        foreach (var resource in resources)
            Visit(resource.Name);
    }

}
