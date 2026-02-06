namespace Qyl.Watchdog.Alerting;

public interface INotificationSender
{
    ValueTask SendAsync(string title, string body, CancellationToken ct);
}
