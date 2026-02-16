using qyl.cli;
using qyl.cli.Commands;
using Spectre.Console;

AnsiConsole.Write(new FigletText("qyl").Color(Color.Orange1));

var cliArgs = CliArgs.Parse([.. Environment.GetCommandLineArgs().Skip(1)]);

return await (cliArgs.Command switch
{
    "init" => InitCommand.ExecuteAsync(cliArgs),
    _ => Task.FromResult(ShowHelp())
});

static int ShowHelp()
{
    AnsiConsole.MarkupLine("[bold]Usage:[/] qyl <command>");
    AnsiConsole.WriteLine();

    var table = new Table().AddColumn("Command").AddColumn("Description");
    table.AddRow("init", "Auto-detect stack and instrument for qyl observability");
    table.AddRow("init dotnet", "Instrument a .NET project");
    table.AddRow("init docker", "Add qyl to docker-compose");
    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Options:[/]");
    AnsiConsole.MarkupLine("  --dry-run                Show changes without applying");
    AnsiConsole.MarkupLine("  --project <path>         Specify project path");
    AnsiConsole.MarkupLine("  --collector-url <url>    Custom collector URL");
    return 0;
}
