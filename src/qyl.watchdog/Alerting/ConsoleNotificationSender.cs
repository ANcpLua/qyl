namespace Qyl.Watchdog.Alerting;

public sealed class ConsoleNotificationSender : INotificationSender
{
    public ValueTask SendAsync(string title, string body, CancellationToken ct)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ALERT: {title} — {body}");
        Console.ResetColor();
        return ValueTask.CompletedTask;
    }
}
