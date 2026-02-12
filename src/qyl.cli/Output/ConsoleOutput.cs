using Spectre.Console;

namespace qyl.cli.Output;

/// <summary>
/// Spectre.Console formatted output for CLI results.
/// </summary>
public static class ConsoleOutput
{
    public static void ShowDryRun(List<string> changes)
    {
        AnsiConsole.MarkupLine("[yellow bold]DRY RUN[/] — the following changes would be applied:");
        AnsiConsole.WriteLine();

        var tree = new Tree("[yellow]Planned Changes[/]");
        foreach (var change in changes)
        {
            tree.AddNode($"[dim]+[/] {change}");
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Run without --dry-run to apply.[/]");
    }

    public static void ShowAppliedChanges(List<string> changes)
    {
        AnsiConsole.WriteLine();

        var tree = new Tree("[green]Applied Changes[/]");
        foreach (var change in changes)
        {
            tree.AddNode($"[green]+[/] {change}");
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }
}
