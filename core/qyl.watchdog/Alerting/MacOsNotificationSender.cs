namespace Qyl.Watchdog.Alerting;

public sealed class MacOsNotificationSender : INotificationSender
{
    public async ValueTask SendAsync(string title, string body, CancellationToken ct)
    {
        var safeTitle = Sanitize(title);
        var safeBody = Sanitize(body);
        var script = $"display notification \"{safeBody}\" with title \"{safeTitle}\" sound name \"Basso\"";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "osascript",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-e");
        process.StartInfo.ArgumentList.Add(script);

        process.Start();
        await process.WaitForExitAsync(ct);
    }

    private static string Sanitize(string input) =>
        input.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}
