// qyl.collector - Startup Banner
// Console output with clickable token URL for terminal login

using System.Runtime.InteropServices;
using Console = System.Console;

namespace qyl.collector;

/// <summary>
/// Startup banner with token URL for qyl.collector.
/// </summary>
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

    /// <summary>
    /// Prints the startup banner to console.
    /// </summary>
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

        // Dashboard URL
        Console.Write("  │  Dashboard:  ");
        Console.ForegroundColor = ConsoleColor.Blue;
        WriteClickableUrl(baseUrl);
        Console.ResetColor();
        PadLine(baseUrl.Length + 13, 68);
        Console.WriteLine("│");

        // Port info
        Console.Write("  │  Port:       ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write(port);
        Console.ResetColor();
        PadLine(port.ToString().Length + 13, 68);
        Console.WriteLine("│");

        Console.WriteLine("  │                                                                   │");
        Console.WriteLine("  ├───────────────────────────────────────────────────────────────────┤");
        Console.WriteLine("  │                                                                   │");

        // Token info
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

        // Clickable URL with token
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

        // Endpoints info
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  Endpoints:");
        Console.WriteLine($"    POST /api/v1/ingest     - qyl. native protocol (primary)");
        Console.WriteLine($"    POST /v1/traces         - OTLP compatibility shim");
        Console.WriteLine($"    GET  /api/v1/sessions   - Query sessions");
        Console.WriteLine($"    GET  /api/v1/live       - SSE live tail");
        Console.WriteLine($"    POST /api/login         - Token authentication");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void WriteClickableUrl(string url)
    {
        // OSC 8 hyperlink escape sequence for clickable URLs in terminals
        // Works in: iTerm2, Windows Terminal, VS Code terminal, many others
        if (IsTerminalSupportsHyperlinks())
        {
            Console.Write($"\u001b]8;;{url}\u001b\\{url}\u001b]8;;\u001b\\");
        }
        else
        {
            Console.Write(url);
        }
    }

    private static bool IsTerminalSupportsHyperlinks()
    {
        // Check for known terminals that support OSC 8 hyperlinks
        var term = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        var wt = Environment.GetEnvironmentVariable("WT_SESSION");

        return !string.IsNullOrEmpty(wt) || // Windows Terminal
               term is "iTerm.app" or "vscode" or "Hyper" ||
               RuntimeInformation.IsOSPlatform(OSPlatform.Linux); // Most modern Linux terminals
    }

    private static void PadLine(int usedChars, int totalWidth)
    {
        var padding = totalWidth - usedChars;
        if (padding > 0)
        {
            Console.Write(new string(' ', padding));
        }
    }
}
