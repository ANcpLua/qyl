// Copyright (c) 2025-2026 ancplua

using System.Globalization;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace Qyl.Run.Internal;

/// <summary>
///     Spectre.Console front-end for the orchestrator. Prints the qyl banner, renders a live
///     table of resource state, and handles <c>[S]</c>/<c>[R]</c>/<c>[B]</c>/<c>[H]</c>/<c>[Esc]</c>
///     key bindings in a secondary task.
/// </summary>
internal sealed class QylConsoleUi(
    IReadOnlyList<QylResource> resources,
    QylResourceRegistry registry,
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
                Repaint(table);
                ctx.Refresh();

                await foreach (var _ in registry.Events.ReadAllAsync(stoppingToken).ConfigureAwait(false))
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
            ResourceLifecycle.Stopped => "[grey]○[/]",
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
                case QylConstants.Keys.Browser: OpenBrowser(); break;
                case QylConstants.Keys.Help: AnsiConsole.MarkupLine(Footer); break;
            }
        }
    }

    private void OpenBrowser()
    {
        var target = resources.FirstOrDefault(r => r.Kind == QylConstants.ResourceKinds.Dashboard);
        if (target is null || !registry.Snapshot.TryGetValue(target.Name, out var state) || state.Endpoint is null)
        {
            AnsiConsole.MarkupLine("[red]No dashboard resource is ready yet.[/]");
            return;
        }

        // UseShellExecute=true dispatches to the OS default handler — opens a browser on
        // Windows / macOS / Linux without the cross-platform branch dance.
        Process.Start(new ProcessStartInfo(state.Endpoint.ToString()) { UseShellExecute = true })?.Dispose();
    }
}
