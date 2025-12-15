using System.Runtime.InteropServices;
using Console = System.Console;

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

    public static void Print(string baseUrl, string token, int port)
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

        Console.Write("  │  Port:       ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(port);
        Console.ResetColor();
        PadLine(port.ToString().Length + 13, 68);
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
        Console.WriteLine("    POST /v1/traces         - OTLP compatibility shim");
        Console.WriteLine("    GET  /api/v1/sessions   - Query sessions");
        Console.WriteLine("    GET  /api/v1/live       - SSE live tail");
        Console.WriteLine("    POST /api/login         - Token authentication");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void WriteClickableUrl(string url)
    {
        if (IsTerminalSupportsHyperlinks())
            Console.Write($"\e]8;;{url}\e\\{url}\e]8;;\e\\");
        else
            Console.Write(url);
    }

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
