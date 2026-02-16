using System.Text.Json;
using qyl.watch;
using Spectre.Console;

var config = CliConfig.Parse(args);
if (config is null)
    return;

var cts = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};
Console.CancelKeyPress += cancelHandler;

AnsiConsole.Write(new FigletText("qyl watch").Color(Color.Orange1));
AnsiConsole.MarkupLine($"[grey]Connecting to {Markup.Escape(config.CollectorUrl)}...[/]");

if (config.Session is not null)
    AnsiConsole.MarkupLine($"[grey]Session filter: {Markup.Escape(config.Session)}[/]");
if (config.ErrorsOnly)
    AnsiConsole.MarkupLine("[yellow]Showing errors only[/]");
if (config.SlowThresholdMs is { } threshold)
    AnsiConsole.MarkupLine($"[yellow]Showing spans slower than {threshold}ms[/]");
if (config.ServiceFilter is { } svc)
    AnsiConsole.MarkupLine($"[cyan]Service filter: {Markup.Escape(svc)}[/]");
if (config.GenAiOnly)
    AnsiConsole.MarkupLine("[magenta]Showing GenAI spans only[/]");

AnsiConsole.MarkupLine("[grey]Press q to quit, c to clear, e to toggle errors, f to cycle services[/]");
AnsiConsole.WriteLine();

var header = new HeaderRenderer();
using var client = new SseClient(config.CollectorUrl);

// Start keyboard handler on a background thread
var keyboardTask = Task.Run(() => HandleKeyboard(config, header, cts.Token, cts.Cancel), CancellationToken.None);

var headerInterval = TimeProvider.System.GetUtcNow();
var spanCount = 0;

try
{
    await foreach (var evt in client.StreamAsync(config.Session, cts.Token))
    {
        switch (evt.Type)
        {
            case "connected":
                AnsiConsole.MarkupLine("[green]Connected to collector[/]");
                AnsiConsole.WriteLine();
                break;

            case "spans":
                ProcessSpanEvent(evt.Data, config, header, ref spanCount, ref headerInterval);
                break;
        }
    }
}
catch (OperationCanceledException)
{
    // Normal shutdown
}

await keyboardTask.ConfigureAwait(false);
Console.CancelKeyPress -= cancelHandler;
cts.Dispose();

AnsiConsole.WriteLine();
AnsiConsole.MarkupLine($"[grey]Disconnected. Total spans received: {spanCount}[/]");

static void ProcessSpanEvent(string data, CliConfig config, HeaderRenderer header, ref int spanCount,
    ref DateTimeOffset headerInterval)
{
    // SSE data is the full TelemetryEventDto: {"eventType":"spans","data":{"spans":[...]},"timestamp":"..."}
    // We need to extract the nested "data" field which contains the SpanBatch.
    SpanBatchDto? batch;
    try
    {
        using var doc = JsonDocument.Parse(data);
        // Try to parse as TelemetryEventDto wrapper first
        if (doc.RootElement.TryGetProperty("data", out var dataElement))
        {
            batch = JsonSerializer.Deserialize<SpanBatchDto>(dataElement.GetRawText());
        }
        else
        {
            // Fallback: data IS the SpanBatch directly
            batch = JsonSerializer.Deserialize<SpanBatchDto>(data);
        }
    }
    catch
    {
        return;
    }

    if (batch?.Spans is not { Count: > 0 })
        return;

    foreach (var span in batch.Spans)
    {
        header.RecordSpan(span);
    }

    SpanRenderer.Render(batch.Spans, config);
    spanCount += batch.Spans.Count;

    // Print header every 5 seconds
    var now = TimeProvider.System.GetUtcNow();
    if ((now - headerInterval).TotalSeconds >= 5)
    {
        AnsiConsole.WriteLine();
        header.Render();
        AnsiConsole.WriteLine();
        headerInterval = now;
    }
}

static void HandleKeyboard(CliConfig config, HeaderRenderer header, CancellationToken token, Action cancel)
{
    while (!token.IsCancellationRequested)
    {
        if (!Console.KeyAvailable)
        {
            Thread.Sleep(50);
            continue;
        }

        var key = Console.ReadKey(true);

        switch (key.KeyChar)
        {
            case 'q':
                cancel();
                return;

            case 'c':
                AnsiConsole.Clear();
                AnsiConsole.Write(new FigletText("qyl watch").Color(Color.Orange1));
                header.Render();
                AnsiConsole.WriteLine();
                break;

            case 'e':
                config.ErrorsOnly = !config.ErrorsOnly;
                AnsiConsole.MarkupLine(config.ErrorsOnly
                    ? "[yellow]Filter: errors only[/]"
                    : "[green]Filter: all spans[/]");
                break;

            case 'f':
                var services = header.GetServiceNames();
                if (services.Count == 0) break;

                if (config.ServiceFilter is null)
                {
                    config.ServiceFilter = services[0];
                }
                else
                {
                    var idx = -1;
                    for (var j = 0; j < services.Count; j++)
                    {
                        if (string.Equals(services[j], config.ServiceFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            idx = j;
                            break;
                        }
                    }

                    config.ServiceFilter = idx < 0 || idx >= services.Count - 1
                        ? null // cycle back to "all"
                        : services[idx + 1];
                }

                AnsiConsole.MarkupLine(config.ServiceFilter is null
                    ? "[green]Service filter: all[/]"
                    : $"[cyan]Service filter: {Markup.Escape(config.ServiceFilter)}[/]");
                break;
        }
    }
}
