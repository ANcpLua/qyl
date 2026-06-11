using System.Text.Json.Nodes;

namespace Qyl.Frankenstein;

internal sealed class TargetAdapter
{
    private static readonly HashSet<string> CodexRootFields = new(StringComparer.Ordinal)
    {
        "name",
        "version",
        "description",
        "spritesheet",
        "atlas",
        "animations",
        "x-frankenstein"
    };

    private static readonly HashSet<string> GenericAgentRootFields = new(StringComparer.Ordinal)
    {
        "name",
        "version",
        "description",
        "spritesheet",
        "atlas",
        "animations",
        "abilities",
        "x-frankenstein"
    };

    private TargetAdapter(string name, bool supportsRootAbilities, HashSet<string> rootFields)
    {
        Name = name;
        SupportsRootAbilities = supportsRootAbilities;
        RootFields = rootFields;
    }

    public string Name { get; }

    public bool SupportsRootAbilities { get; }

    private HashSet<string> RootFields { get; }

    public static TargetAdapter Resolve(string target)
    {
        return target switch
        {
            "codex" => new TargetAdapter("codex", supportsRootAbilities: false, CodexRootFields),
            "generic-agent" => new TargetAdapter("generic-agent", supportsRootAbilities: true, GenericAgentRootFields),
            _ => throw new FrankensteinException($"unknown target adapter: {target}")
        };
    }

    public IReadOnlyList<PetIssue> ValidateRoot(JsonObject petJson)
    {
        var issues = new List<PetIssue>();
        foreach (var (name, _) in petJson)
        {
            if (RootFields.Contains(name))
            {
                continue;
            }

            if (string.Equals(name, "abilities", StringComparison.Ordinal) && !SupportsRootAbilities)
            {
                issues.Add(PetIssue.ForRepair(
                    "Frankenstein ability metadata unsupported by Codex target",
                    "target adapter",
                    "pet.json",
                    "abilities",
                    "root field",
                    "x-frankenstein.abilities",
                    "x-frankenstein.abilities",
                    "target codex does not support Frankenstein ability metadata as root schema",
                    "Codex exporter",
                    RepairKind.MoveAbilitiesToExtension));
                continue;
            }

            issues.Add(PetIssue.NotRepairable(
                "Unsupported target root field",
                "target adapter",
                "pet.json",
                name,
                $"one of: {string.Join(", ", RootFields)}",
                $"{Name} exporter"));
        }

        return issues;
    }
}
