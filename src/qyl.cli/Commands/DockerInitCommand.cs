using qyl.cli.Detection;
using qyl.cli.Output;
using Spectre.Console;

namespace qyl.cli.Commands;

/// <summary>
///     Adds qyl collector service to a Docker Compose file.
/// </summary>
public static class DockerInitCommand
{
    public static Task<int> ExecuteAsync(CliArgs args)
    {
        var workingDir = args.ProjectPath ?? Directory.GetCurrentDirectory();
        var composePath = FindComposeFile(workingDir);

        if (composePath is null)
        {
            AnsiConsole.MarkupLine("[red]No docker-compose file found.[/]");
            AnsiConsole.MarkupLine("Expected: docker-compose.yml, docker-compose.yaml, or compose.yaml");
            return Task.FromResult(1);
        }

        AnsiConsole.MarkupLine($"[blue]Compose file:[/] {composePath}");

        var editor = new ComposeEditor(composePath);

        if (editor.HasQylService())
        {
            AnsiConsole.MarkupLine("[green]qyl service already present in compose file[/]");
            return Task.FromResult(0);
        }

        var changes = editor.GetPlannedChanges();

        if (args.DryRun)
        {
            ConsoleOutput.ShowDryRun(changes);
            return Task.FromResult(0);
        }

        editor.Apply();

        ConsoleOutput.ShowAppliedChanges(changes);
        AnsiConsole.MarkupLine("[green]Done! Run docker compose up — qyl collector starts alongside your services.[/]");
        return Task.FromResult(0);
    }

    private static string? FindComposeFile(string directory)
    {
        string[] candidates = ["docker-compose.yml", "docker-compose.yaml", "compose.yaml"];

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(directory, candidate);
            if (File.Exists(path))
            {
                return Path.GetFullPath(path);
            }
        }

        return null;
    }
}
