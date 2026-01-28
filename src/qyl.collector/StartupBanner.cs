namespace qyl.collector;

public static class StartupBanner
{
    private const string Logo = """
                                   ██████╗ ██╗   ██╗██╗
                                  ██╔═══██╗╚██╗ ██╔╝██║
                                  ██║   ██║ ╚████╔╝ ██║
                                  ██║▄▄ ██║  ╚██╔╝  ██║
                                  ╚██████╔╝   ██║   ███████╗
                                   ╚══▀▀═╝    ╚═╝   ╚══════╝
                                """;

    public static void Print(
        string baseUrl,
        string token,
        int port,
        int grpcPort = 4317,
        OtlpCorsOptions? corsOptions = null,
        OtlpApiKeyOptions? apiKeyOptions = null)
    {
        var tokenUrl = $"{baseUrl}?t={token}";

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Logo);
        Console.ResetColor();
        Console.WriteLine();

        Console.WriteLine("  ╭───────────────────────────────────────────────────────────────────╮");
        Console.WriteLine("  │                                                                   │");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  │  ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("qyl.collector");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(" is running                                       │");
        Console.ResetColor();
        Console.WriteLine("  │                                                                   │");

        Console.Write("  │  Dashboard:  ");
        Console.ForegroundColor = ConsoleColor.Blue;
        WriteClickableUrl(baseUrl);
        Console.ResetColor();
        PadLine(baseUrl.Length + 13, 68);
        Console.WriteLine("│");

        Console.Write("  │  HTTP Port:  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(port);
        Console.ResetColor();
        PadLine(port.ToString().Length + 13, 68);
        Console.WriteLine("│");

        Console.Write("  │  gRPC Port:  ");
        Console.ForegroundColor = grpcPort > 0 ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
        var grpcText = grpcPort > 0 ? grpcPort.ToString() : "disabled";
        Console.Write(grpcText);
        Console.ResetColor();
        PadLine(grpcText.Length + 13, 68);
        Console.WriteLine("│");

        Console.WriteLine("  │                                                                   │");
        Console.WriteLine("  ├───────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("  │                                                                   │");

        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("  │  ");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Login Token:");
        Console.ResetColor();
        Console.WriteLine("                                                  │");
        Console.Write("  │  ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(token);
        Console.ResetColor();
        PadLine(token.Length + 2, 68);
        Console.WriteLine("│");

        Console.WriteLine("  │                                                                   │");

        Console.Write("  │  ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("Click to login: ");
        Console.ResetColor();
        Console.WriteLine("                                               │");
        Console.Write("  │  ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        WriteClickableUrl(tokenUrl);
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  │                                                                   │");

        Console.WriteLine("  ╰───────────────────────────────────────────────────────────────────╯");
        Console.WriteLine();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Endpoints:");
        Console.WriteLine("    POST /api/v1/ingest     - qyl. native protocol (primary)");
        Console.WriteLine("    POST /v1/traces         - OTLP HTTP compatibility shim");
        if (grpcPort > 0)
            Console.WriteLine($"    gRPC TraceService       - OTLP gRPC (port {grpcPort})");
        Console.WriteLine("    GET  /api/v1/sessions   - Query sessions");
        Console.WriteLine("    GET  /api/v1/live       - SSE live tail");
        Console.WriteLine("    POST /api/login         - Token authentication");
        Console.ResetColor();
        Console.WriteLine();

        // OTLP CORS/Auth status
        if (corsOptions?.IsEnabled == true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  OTLP CORS: ");
            Console.ResetColor();
            Console.WriteLine(corsOptions.AllowedOrigins);
        }

        if (apiKeyOptions?.IsApiKeyMode == true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  OTLP Auth: API Key required (x-otlp-api-key header)");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  OTLP Auth: UNSECURED - telemetry accepted without authentication");
            Console.ResetColor();
        }

        Console.WriteLine();
    }

    private static void WriteClickableUrl(string url) =>
        Console.Write(IsTerminalSupportsHyperlinks() ? $"\e]8;;{url}\e\\{url}\e]8;;\e\\" : url);

    private static bool IsTerminalSupportsHyperlinks()
    {
        var term = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var wt = Environment.GetEnvironmentVariable("WT_SESSION");

        return !string.IsNullOrEmpty(wt) ||
               term is "iTerm.app" or "vscode" or "Hyper" ||
               RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    }

    private static void PadLine(int usedChars, int totalWidth)
    {
        var padding = totalWidth - usedChars;
        if (padding > 0) Console.Write(new string(' ', padding));
    }
}
