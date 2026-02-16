namespace Qyl.Watchdog;

/// <summary>
///     Manages macOS launchd agent for auto-start on login.
///     Creates ~/Library/LaunchAgents/com.qyl.watchdog.plist pointing to the global tool.
/// </summary>
public static class LaunchdSetup
{
    private const string Label = "com.qyl.watchdog";

    private static readonly string SPlistPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", $"{Label}.plist");

    private static readonly string SLogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".qyl", "logs");

    public static async Task InstallAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            await Console.Error.WriteLineAsync("--install is only supported on macOS (launchd)");
            return;
        }

        var toolPath = FindToolPath();
        if (toolPath is null)
        {
            await Console.Error.WriteLineAsync("qyl-watchdog not found in PATH. Install it first:");
            await Console.Error.WriteLineAsync("  dotnet pack src/qyl.watchdog -c Release");
            await Console.Error.WriteLineAsync("  dotnet tool install --global --add-source ./artifacts qyl.watchdog");
            return;
        }

        Directory.CreateDirectory(SLogDir);
        var plistDir = Path.GetDirectoryName(SPlistPath);
        if (plistDir is not null)
            Directory.CreateDirectory(plistDir);

        var plist = GeneratePlist(toolPath);
        await File.WriteAllTextAsync(SPlistPath, plist);

        // Unload first if already loaded (idempotent)
        await RunAsync("launchctl", $"bootout gui/{GetUid()} {SPlistPath}");
        var result = await RunAsync("launchctl", $"bootstrap gui/{GetUid()} {SPlistPath}");

        if (result == 0)
        {
            await Console.Out.WriteLineAsync($"Installed and started launchd agent: {Label}");
            await Console.Out.WriteLineAsync($"  Plist: {SPlistPath}");
            await Console.Out.WriteLineAsync($"  Logs:  {SLogDir}/");
            await Console.Out.WriteLineAsync($"  Tool:  {toolPath}");
            await Console.Out.WriteLineAsync();
            await Console.Out.WriteLineAsync("qyl-watchdog will now start automatically on login.");
            await Console.Out.WriteLineAsync("Use 'qyl-watchdog --uninstall' to remove.");
        }
        else
        {
            await Console.Error.WriteLineAsync($"Failed to load launchd agent (exit code {result})");
        }
    }

    public static async Task UninstallAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            await Console.Error.WriteLineAsync("--uninstall is only supported on macOS (launchd)");
            return;
        }

        if (!File.Exists(SPlistPath))
        {
            await Console.Out.WriteLineAsync("No launchd agent installed.");
            return;
        }

        await RunAsync("launchctl", $"bootout gui/{GetUid()} {SPlistPath}");
        File.Delete(SPlistPath);

        await Console.Out.WriteLineAsync($"Uninstalled launchd agent: {Label}");
        await Console.Out.WriteLineAsync($"  Removed: {SPlistPath}");
    }

    public static async Task StatusAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            await Console.Error.WriteLineAsync("--status is only supported on macOS (launchd)");
            return;
        }

        if (!File.Exists(SPlistPath))
        {
            await Console.Out.WriteLineAsync("Not installed as launchd agent.");
            return;
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "launchctl", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("print");
        process.StartInfo.ArgumentList.Add($"gui/{GetUid()}/{Label}");

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            await Console.Out.WriteLineAsync($"Agent: {Label}");
            await Console.Out.WriteLineAsync($"Plist: {SPlistPath}");

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWithOrdinal("pid") ||
                    trimmed.StartsWithOrdinal("state") ||
                    trimmed.StartsWithOrdinal("last exit"))
                {
                    await Console.Out.WriteLineAsync($"  {trimmed}");
                }
            }
        }
        else
        {
            await Console.Out.WriteLineAsync("Agent installed but not currently loaded.");
        }
    }

    private static string GeneratePlist(string toolPath) => $"""
                                                             <?xml version="1.0" encoding="UTF-8"?>
                                                             <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                                                             <plist version="1.0">
                                                             <dict>
                                                                 <key>Label</key>
                                                                 <string>{Label}</string>
                                                                 <key>ProgramArguments</key>
                                                                 <array>
                                                                     <string>{toolPath}</string>
                                                                 </array>
                                                                 <key>RunAtLoad</key>
                                                                 <true/>
                                                                 <key>KeepAlive</key>
                                                                 <dict>
                                                                     <key>SuccessfulExit</key>
                                                                     <false/>
                                                                 </dict>
                                                                 <key>ThrottleInterval</key>
                                                                 <integer>10</integer>
                                                                 <key>StandardOutPath</key>
                                                                 <string>{SLogDir}/watchdog.log</string>
                                                                 <key>StandardErrorPath</key>
                                                                 <string>{SLogDir}/watchdog.err</string>
                                                                 <key>ProcessType</key>
                                                                 <string>Background</string>
                                                                 <key>LowPriorityBackgroundIO</key>
                                                                 <true/>
                                                             </dict>
                                                             </plist>
                                                             """;

    private static string? FindToolPath()
    {
        var dotnetToolsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet", "tools", "qyl-watchdog");

        if (File.Exists(dotnetToolsPath))
            return dotnetToolsPath;

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "which", RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("qyl-watchdog");
        process.Start();
        var path = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        return process.ExitCode == 0 && !string.IsNullOrEmpty(path) ? path : null;
    }

    private static string GetUid()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "id", ArgumentList = { "-u" }, RedirectStandardOutput = true, UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start 'id' process");

        var uid = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return uid;
    }

    private static async Task<int> RunAsync(string command, string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
