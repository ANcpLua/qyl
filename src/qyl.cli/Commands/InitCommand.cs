using qyl.cli.Detection;
using Spectre.Console;

namespace qyl.cli.Commands;

/// <summary>
/// Router for 'qyl init' — dispatches to stack-specific commands or auto-detects.
/// </summary>
public static class InitCommand
{
    public static async Task<int> ExecuteAsync(CliArgs args)
    {
        return args.SubCommand switch
        {
            "dotnet" => await DotnetInitCommand.ExecuteAsync(args),
            "docker" => await DockerInitCommand.ExecuteAsync(args),
            null => await AutoDetectAndExecuteAsync(args),
            _ => UnknownStack(args.SubCommand),
        };
    }

    private static async Task<int> AutoDetectAndExecuteAsync(CliArgs args)
    {
        var workingDir = args.ProjectPath ?? Directory.GetCurrentDirectory();
        var stack = StackDetector.Detect(workingDir);

        switch (stack)
        {
            case DetectedStack.Dotnet:
                AnsiConsole.MarkupLine("[blue]Detected:[/] .NET project");
                return await DotnetInitCommand.ExecuteAsync(args);

            case DetectedStack.Docker:
                AnsiConsole.MarkupLine("[blue]Detected:[/] Docker Compose");
                return await DockerInitCommand.ExecuteAsync(args);

            case DetectedStack.Node:
                AnsiConsole.MarkupLine("[blue]Detected:[/] Node.js project");
                AnsiConsole.MarkupLine("[yellow]Node.js auto-instrumentation is not yet supported.[/]");
                AnsiConsole.MarkupLine("Install the OpenTelemetry JS SDK manually:");
                AnsiConsole.MarkupLine("  npm install @opentelemetry/sdk-node @opentelemetry/exporter-trace-otlp-grpc");
                AnsiConsole.MarkupLine("  Set OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317");
                return 0;

            case DetectedStack.Python:
                AnsiConsole.MarkupLine("[blue]Detected:[/] Python project");
                AnsiConsole.MarkupLine("[yellow]Python auto-instrumentation is not yet supported.[/]");
                AnsiConsole.MarkupLine("Install the OpenTelemetry Python SDK manually:");
                AnsiConsole.MarkupLine("  pip install opentelemetry-sdk opentelemetry-exporter-otlp");
                AnsiConsole.MarkupLine("  Set OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317");
                return 0;

            default:
                AnsiConsole.MarkupLine("[yellow]Could not detect project stack.[/]");
                AnsiConsole.MarkupLine("Specify a stack explicitly:");
                AnsiConsole.MarkupLine("  qyl init dotnet");
                AnsiConsole.MarkupLine("  qyl init docker");
                return 1;
        }
    }

    private static int UnknownStack(string stack)
    {
        AnsiConsole.MarkupLine($"[red]Unknown stack:[/] {stack}");
        AnsiConsole.MarkupLine("Supported stacks: dotnet, docker");
        return 1;
    }
}
