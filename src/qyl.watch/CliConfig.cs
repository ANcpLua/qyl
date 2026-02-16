namespace qyl.watch;

/// <summary>
///     CLI argument parsing and filter configuration.
///     Mutable properties allow keyboard toggle of filters at runtime.
/// </summary>
internal sealed class CliConfig
{
    public string CollectorUrl { get; init; } = "http://localhost:5100";
    public string? Session { get; init; }
    public bool ErrorsOnly { get; set; }
    public int? SlowThresholdMs { get; init; }
    public string? ServiceFilter { get; set; }
    public bool GenAiOnly { get; init; }

    public static CliConfig? Parse(string[] args)
    {
        var url = "http://localhost:5100";
        string? session = null;
        var errorsOnly = false;
        int? slowMs = null;
        string? service = null;
        var genai = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--url" when i + 1 < args.Length:
                    url = args[++i].TrimEnd('/');
                    break;
                case "--session" when i + 1 < args.Length:
                    session = args[++i];
                    break;
                case "--errors":
                    errorsOnly = true;
                    break;
                case "--slow" when i + 1 < args.Length && int.TryParse(args[i + 1], out var ms):
                    slowMs = ms;
                    i++;
                    break;
                case "--slow":
                    slowMs = 200;
                    break;
                case "--service" when i + 1 < args.Length:
                    service = args[++i];
                    break;
                case "--genai":
                    genai = true;
                    break;
                case "--help" or "-h":
                    PrintHelp();
                    return null;
            }
        }

        return new CliConfig
        {
            CollectorUrl = url,
            Session = session,
            ErrorsOnly = errorsOnly,
            SlowThresholdMs = slowMs,
            ServiceFilter = service,
            GenAiOnly = genai
        };
    }

    private static void PrintHelp() =>
        Console.WriteLine("""
                          qyl-watch -- Live Terminal Observability

                          Usage:
                            qyl-watch                           Show all spans
                            qyl-watch --url http://host:5100    Custom collector URL
                            qyl-watch --errors                  Errors only
                            qyl-watch --slow [ms]               Slow spans (default >200ms)
                            qyl-watch --service my-api           Filter by service name
                            qyl-watch --genai                   GenAI spans only
                            qyl-watch --session abc123           Filter by session

                          Keyboard:
                            q / Ctrl+C    Quit
                            c             Clear screen
                            e             Toggle errors-only filter
                            f             Cycle service filter
                          """);
}
