using System.Globalization;
using System.Text.Json.Nodes;

namespace Qyl.Frankenstein;

internal enum RepairKind
{
    AnimationRow,
    MoveAbilitiesToExtension
}

internal sealed record PetIssue(
    string Title,
    string Layer,
    string File,
    string Path,
    string Current,
    string Expected,
    string? Proposed,
    string Reason,
    string Owner,
    bool Repairable,
    RepairKind? RepairKind)
{
    public string Message => Path.Length is 0
        ? $"{Title}: {File}"
        : $"{Title}: {File} {Path} is {Current}, expected {Expected}";

    public static PetIssue ForRepair(
        string title,
        string layer,
        string file,
        string path,
        string current,
        string expected,
        string? proposed,
        string reason,
        string owner,
        RepairKind repairKind) =>
        new(title, layer, file, path, current, expected, proposed, reason, owner, true, repairKind);

    public static PetIssue NotRepairable(
        string title,
        string layer,
        string file,
        string current,
        string expected,
        string owner) =>
        new(title, layer, file, string.Empty, current, expected, null, title, owner, false, null);
}

internal sealed record RepairAction(
    RepairKind Kind,
    string File,
    string Path,
    string? From,
    string? To,
    string Reason)
{
    public JsonObject ToManifestJson()
    {
        var output = new JsonObject
        {
            ["file"] = File,
            ["path"] = Path
        };

        output["from"] = TryInt(From, out var from) ? from : From;
        output["to"] = TryInt(To, out var to) ? to : To;
        output["reason"] = Reason;
        return output;
    }

    private static bool TryInt(string? value, out int parsed) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
}

internal static class RepairPlanner
{
    public static IReadOnlyList<RepairAction> Plan(Inspection inspection, TargetAdapter target)
    {
        var actions = new List<RepairAction>();
        foreach (var issue in inspection.Errors)
        {
            if (!issue.Repairable || issue.RepairKind is null)
            {
                continue;
            }

            if (issue.RepairKind is RepairKind.AnimationRow && issue.Proposed is not null)
            {
                actions.Add(new RepairAction(
                    RepairKind.AnimationRow,
                    issue.File,
                    issue.Path,
                    issue.Current,
                    issue.Proposed,
                    "row index exceeded atlas row range"));
            }

            if (issue.RepairKind is RepairKind.MoveAbilitiesToExtension && !target.SupportsRootAbilities)
            {
                actions.Add(new RepairAction(
                    RepairKind.MoveAbilitiesToExtension,
                    issue.File,
                    issue.Path,
                    "root field",
                    "x-frankenstein.abilities",
                    $"target {target.Name} does not support Frankenstein ability metadata as root schema"));
            }
        }

        return actions;
    }

    public static int? FindCandidateRow(
        AnimationContract invalidAnimation,
        AtlasContract atlas,
        SortedDictionary<string, AnimationContract> animations,
        IReadOnlyList<RowEvidence> rowEvidence)
    {
        var usedRows = animations.Values
            .Where(animation => animation.Row >= 0 && animation.Row < atlas.Rows)
            .Select(static animation => animation.Row)
            .ToHashSet();

        var occupied = rowEvidence
            .Where(row => row.NonEmpty && row.NonEmptyFrames >= invalidAnimation.Frames && !usedRows.Contains(row.Row))
            .OrderBy(static row => row.Row)
            .FirstOrDefault();

        if (occupied is not null)
        {
            return occupied.Row;
        }

        var firstFree = Enumerable.Range(0, atlas.Rows).FirstOrDefault(row => !usedRows.Contains(row), -1);
        return firstFree >= 0 ? firstFree : null;
    }
}
