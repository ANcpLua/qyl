using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace Qyl.Host.Internal;

internal sealed class QylConsoleUi(
    IReadOnlyList<QylResource> resources,
    QylResourceRegistry registry,
    QylResourceActions resourceActions,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    private const string Footer =
        "[grey][[S]] Stop   [[R]] Restart   [[B]] Open browser   [[H]] Help   [[Esc]] Exit[/]";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RenderBanner();

        var keyTask = Task.Run(() => HandleKeysAsync(stoppingToken), stoppingToken);
        var table = BuildTable();

        await AnsiConsole.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .StartAsync(async ctx =>
            {
                // Subscribe first, then paint the snapshot: no event can slip between the two.
                using var subscription = registry.Subscribe();
                Repaint(table);
                ctx.Refresh();

                await foreach (var _ in subscription.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
                {
                    Repaint(table);
                    ctx.Refresh();
                }
            }).ConfigureAwait(false);

        await keyTask.ConfigureAwait(false);
    }

    private static void RenderBanner()
    {
        AnsiConsole.Write(new FigletText(QylConstants.Product.Banner).Color(Color.Aqua));
        AnsiConsole.MarkupLine($"[grey]v{QylConstants.Product.Version} — {QylConstants.Product.Tagline}[/]");
        AnsiConsole.WriteLine();
    }

    private Table BuildTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn(string.Empty))
            .AddColumn(new TableColumn("[bold]Status[/]").Centered())
            .AddColumn(new TableColumn("[bold]Port[/]").Centered())
            .AddColumn(new TableColumn("[bold]Endpoint[/]"));

        foreach (var _ in resources) table.AddEmptyRow();
        return table;
    }

    private void Repaint(Table table)
    {
        for (var i = 0; i < resources.Count; i++)
        {
            var r = resources[i];
            registry.Snapshot.TryGetValue(r.Name, out var state);
            var lifecycle = state?.Lifecycle ?? ResourceLifecycle.Pending;

            table.UpdateCell(i, 0, new Markup($"[bold]{Markup.Escape(r.Name)}[/]"));
            table.UpdateCell(i, 1, new Markup(StatusDot(lifecycle)));
            table.UpdateCell(i, 2, new Markup(PortText(r, state)));
            table.UpdateCell(i, 3,
                new Markup(state?.Endpoint?.ToString() is { } u ? $"[link={u}]{Markup.Escape(u)}[/]" : "[grey]—[/]"));
        }
    }

    private static string StatusDot(ResourceLifecycle lifecycle)
    {
        return lifecycle switch
        {
            ResourceLifecycle.Ready => "[green]●[/]",
            ResourceLifecycle.Starting => "[yellow]◐[/]",
            ResourceLifecycle.Stopping => "[yellow]◑[/]",
            ResourceLifecycle.Failed => "[red]✖[/]",
            _ => "[grey]○[/]"
        };
    }

    private static string PortText(QylResource resource, QylResourceState? state)
    {
        var port = state?.AllocatedPort ??
                   (resource.Port == QylConstants.Ports.DynamicAllocation ? null : resource.Port);
        return port?.ToString(CultureInfo.InvariantCulture) ?? "[grey]auto[/]";
    }

    private async Task HandleKeysAsync(CancellationToken stoppingToken)
    {
        // Non-interactive host (CI, piped stdout, detached, `dotnet run` capture): Console.KeyAvailable
        // throws on redirected input, so there is no keyboard control surface. Idle until shutdown and let
        // the /runner API + live table be the surface.
        if (Console.IsInputRedirected)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // shutdown requested
            }

            return;
        }

        AnsiConsole.MarkupLine(Footer);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(100, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape)
            {
                lifetime.StopApplication();
                return;
            }

            switch (char.ToUpperInvariant(key.KeyChar))
            {
                case QylConstants.Keys.Stop:
                    lifetime.StopApplication();
                    return;
                case QylConstants.Keys.Restart: await RequestRestartsAsync(stoppingToken).ConfigureAwait(false); break;
                case QylConstants.Keys.Browser: OpenBrowser(); break;
                case QylConstants.Keys.Help: AnsiConsole.MarkupLine(Footer); break;
            }
        }
    }

    // [B] opens the thing a developer is iterating on: the first Ready dev-command resource (a Vite
    // dashboard in `--dev`) wins over the collector's embedded dashboard; with no command resources
    // the first Ready endpoint (the collector, which serves the built dashboard) opens instead.
    private void OpenBrowser()
    {
        var endpoint = resources
            .OrderBy(static r => r.Kind == QylResourceKind.Command ? 0 : 1)
            .Select(r => registry.Snapshot.GetValueOrDefault(r.Name))
            .FirstOrDefault(static s => s is { Lifecycle: ResourceLifecycle.Ready, Endpoint: not null })
            ?.Endpoint;

        if (endpoint is null)
        {
            AnsiConsole.MarkupLine("[yellow]No ready resource with an endpoint to open yet.[/]");
            return;
        }

        try
        {
            var url = endpoint.ToString();
            var start = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };
            if (OperatingSystem.IsWindows())
            {
                start.FileName = "cmd";
                start.ArgumentList.Add("/c");
                start.ArgumentList.Add("start");
                start.ArgumentList.Add(string.Empty);
                start.ArgumentList.Add(url);
            }
            else
            {
                start.FileName = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
                start.ArgumentList.Add(url);
            }

            using var _ = Process.Start(start);
            AnsiConsole.MarkupLine($"[aqua]Opening {Markup.Escape(url)}[/]");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            AnsiConsole.MarkupLine($"[yellow]Could not open browser: {Markup.Escape(ex.Message)}[/]");
        }
    }

    // The table has no row selection, so [R] requests every launched process resource. The same
    // acknowledged orchestrator path backs the loopback HTTP action endpoint.
    private async Task RequestRestartsAsync(CancellationToken cancellationToken)
    {
        var accepted = 0;
        foreach (var resource in resources.Where(static resource => resource.Launch is not null))
        {
            var result = await resourceActions.RequestAsync(
                resource.Name, QylResourceAction.Restart, cancellationToken).ConfigureAwait(false);
            if (result.Status == QylResourceActionStatus.Accepted) accepted++;
        }

        AnsiConsole.MarkupLine(accepted == 0
            ? "[yellow]No ready process resources accepted a restart.[/]"
            : $"[aqua]Restart accepted for {accepted} process resource(s).[/]");
    }
}
