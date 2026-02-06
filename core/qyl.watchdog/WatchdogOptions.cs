namespace Qyl.Watchdog;

public sealed record WatchdogOptions
{
    public int IntervalMs { get; init; } = 5_000;
    public double SpikeThreshold { get; init; } = 3.0;
    public int SustainedCount { get; init; } = 6;
    public long CooldownMs { get; init; } = 300_000;
    public string? OtlpEndpoint { get; init; }
    public HashSet<string> IgnoreProcesses { get; init; } = [];
    public bool Verbose { get; init; }
    public bool Once { get; init; }
    public int? KillPid { get; init; }
    public bool Install { get; init; }
    public bool Uninstall { get; init; }
    public bool Status { get; init; }

    public static WatchdogOptions Parse(string[] args)
    {
        var options = FromEnvironment();

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--interval" when TryNext(args, ref i, out var v) && int.TryParse(v, out var ms):
                    options = options with { IntervalMs = ms };
                    break;
                case "--threshold" when TryNext(args, ref i, out var v) && double.TryParse(v, out var t):
                    options = options with { SpikeThreshold = t };
                    break;
                case "--sustained" when TryNext(args, ref i, out var v) && int.TryParse(v, out var s):
                    options = options with { SustainedCount = s };
                    break;
                case "--cooldown" when TryNext(args, ref i, out var v) && long.TryParse(v, out var c):
                    options = options with { CooldownMs = c };
                    break;
                case "--otlp" when TryNext(args, ref i, out var v):
                    options = options with { OtlpEndpoint = v };
                    break;
                case "--ignore" when TryNext(args, ref i, out var v):
                    foreach (var name in v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        options.IgnoreProcesses.Add(name);
                    break;
                case "--verbose" or "-v":
                    options = options with { Verbose = true };
                    break;
                case "--once":
                    options = options with { Once = true };
                    break;
                case "--kill" when TryNext(args, ref i, out var v) && int.TryParse(v, out var pid):
                    options = options with { KillPid = pid };
                    break;
                case "--install":
                    options = options with { Install = true };
                    break;
                case "--uninstall":
                    options = options with { Uninstall = true };
                    break;
                case "--status":
                    options = options with { Status = true };
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
            }
        }

        return options;
    }

    private static WatchdogOptions FromEnvironment()
    {
        var options = new WatchdogOptions();

        if (int.TryParse(Env("QYL_WATCH_INTERVAL"), out var interval))
            options = options with { IntervalMs = interval };
        if (double.TryParse(Env("QYL_WATCH_THRESHOLD"), out var threshold))
            options = options with { SpikeThreshold = threshold };
        if (int.TryParse(Env("QYL_WATCH_SUSTAINED"), out var sustained))
            options = options with { SustainedCount = sustained };
        if (long.TryParse(Env("QYL_WATCH_COOLDOWN"), out var cooldown))
            options = options with { CooldownMs = cooldown };

        var otlp = Env("QYL_WATCH_OTLP_ENDPOINT");
        if (!string.IsNullOrEmpty(otlp))
            options = options with { OtlpEndpoint = otlp };

        var ignore = Env("QYL_WATCH_IGNORE");
        if (!string.IsNullOrEmpty(ignore))
        {
            foreach (var name in ignore.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                options.IgnoreProcesses.Add(name);
        }

        return options;
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static bool TryNext(IReadOnlyList<string> args, ref int i, out string value)
    {
        if (i + 1 < args.Count)
        {
            value = args[++i];
            return true;
        }

        value = "";
        return false;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            qyl-watch — system resource watchdog

            Usage: qyl-watch [options]

            Options:
              --interval <ms>     Poll interval (default: 5000)
              --threshold <mult>  Spike multiplier (default: 3.0)
              --sustained <n>     Samples before alert (default: 6)
              --cooldown <ms>     Alert cooldown (default: 300000)
              --otlp <url>        OTLP endpoint for export
              --ignore <names>    Comma-separated process names to ignore
              --verbose, -v       Detailed output
              --once              Single scan then exit
              --kill <pid>        Kill a process by PID
              --install           Install as launchd agent (auto-start on login)
              --uninstall         Remove launchd agent
              --status            Show launchd agent status
              --help, -h          Show this help

            Environment:
              QYL_WATCH_INTERVAL, QYL_WATCH_THRESHOLD, QYL_WATCH_SUSTAINED,
              QYL_WATCH_COOLDOWN, QYL_WATCH_OTLP_ENDPOINT, QYL_WATCH_IGNORE
            """);
    }
}
