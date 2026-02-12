namespace qyl.cli;

/// <summary>
/// Parsed CLI arguments for the qyl tool.
/// </summary>
public sealed class CliArgs
{
    public string Command { get; private init; } = "";
    public string? SubCommand { get; private init; }
    public string? ProjectPath { get; private init; }
    public string? CollectorUrl { get; private init; }
    public bool DryRun { get; private init; }

    public static CliArgs Parse(string[] args)
    {
        string? command = null;
        string? subCommand = null;
        string? projectPath = null;
        string? collectorUrl = null;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--project" when i + 1 < args.Length:
                    projectPath = args[++i];
                    break;
                case "--collector-url" when i + 1 < args.Length:
                    collectorUrl = args[++i];
                    break;
                default:
                    if (command is null)
                    {
                        command = args[i];
                    }
                    else if (subCommand is null)
                    {
                        subCommand = args[i];
                    }

                    break;
            }
        }

        return new CliArgs
        {
            Command = command ?? "",
            SubCommand = subCommand,
            ProjectPath = projectPath,
            CollectorUrl = collectorUrl,
            DryRun = dryRun,
        };
    }
}
