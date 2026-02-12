using qyl.cli.Detection;
using qyl.cli.Output;
using Spectre.Console;

namespace qyl.cli.Commands;

/// <summary>
/// Instruments a .NET project with qyl.servicedefaults.
/// </summary>
public static class DotnetInitCommand
{
    public static async Task<int> ExecuteAsync(CliArgs args)
    {
        var workingDir = args.ProjectPath ?? Directory.GetCurrentDirectory();
        var csprojPath = FindCsproj(workingDir);

        if (csprojPath is null)
        {
            AnsiConsole.MarkupLine("[red]No .csproj found.[/] Specify with --project <path>");
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Project:[/] {csprojPath}");

        var editor = new CsprojEditor(csprojPath);

        if (editor.HasReference("qyl.servicedefaults"))
        {
            AnsiConsole.MarkupLine("[green]Already instrumented with qyl.servicedefaults[/]");
            return 0;
        }

        var changes = new List<string>();
        changes.Add("Add PackageReference: qyl.servicedefaults");

        var programCsPath = Path.Combine(Path.GetDirectoryName(csprojPath)!, "Program.cs");
        var programCsPatched = false;

        if (File.Exists(programCsPath))
        {
            var patcher = new ProgramCsEditor(programCsPath);
            if (patcher.NeedsServiceDefaults())
            {
                changes.Add("Inject builder.AddServiceDefaults() into Program.cs");
                programCsPatched = true;
            }
        }

        if (args.DryRun)
        {
            ConsoleOutput.ShowDryRun(changes);
            return 0;
        }

        editor.AddPackageReference("qyl.servicedefaults");

        if (programCsPatched)
        {
            var patcher = new ProgramCsEditor(programCsPath);
            patcher.InjectServiceDefaults();
        }

        if (args.CollectorUrl is not null)
        {
            await HealthCheckAsync(args.CollectorUrl);
        }

        ConsoleOutput.ShowAppliedChanges(changes);
        AnsiConsole.MarkupLine("[green]Done! Run your app — telemetry flows to qyl automatically.[/]");
        return 0;
    }

    private static string? FindCsproj(string directory)
    {
        if (File.Exists(directory) && directory.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFullPath(directory);
        }

        if (Directory.Exists(directory))
        {
            var files = Directory.GetFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly);
            if (files.Length == 1)
            {
                return Path.GetFullPath(files[0]);
            }

            if (files.Length > 1)
            {
                AnsiConsole.MarkupLine("[yellow]Multiple .csproj files found. Specify one with --project[/]");
                foreach (var f in files)
                {
                    AnsiConsole.MarkupLine($"  {Path.GetFileName(f)}");
                }

                return null;
            }

            // Search one level deep
            files = Directory.GetFiles(directory, "*.csproj", SearchOption.AllDirectories);
            if (files.Length == 1)
            {
                return Path.GetFullPath(files[0]);
            }
        }

        return null;
    }

    private static async Task HealthCheckAsync(string collectorUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(new Uri(new Uri(collectorUrl), "/health"));
            if (response.IsSuccessStatusCode)
            {
                AnsiConsole.MarkupLine($"[green]Collector at {collectorUrl} is healthy[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[yellow]Collector at {collectorUrl} returned {response.StatusCode}[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not reach collector at {collectorUrl}: {ex.Message}[/]");
        }
    }
}
