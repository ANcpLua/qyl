using System.Net;
using System.Net.Sockets;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[Collection(RunnerNetworkTestGroup.Name)]
public sealed class ConsoleUiTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Redirected_or_noninteractive_output_selects_the_headless_ui(
        bool outputRedirected,
        bool interactive)
    {
        Assert.True(QylConsoleUi.ShouldUseHeadlessUi(outputRedirected, interactive));
    }

    [Fact]
    public async Task Headless_in_process_host_stays_alive_and_serves_the_runner_api()
    {
        Assert.True(
            QylConsoleUi.ShouldUseHeadlessUi(
                Console.IsOutputRedirected,
                Spectre.Console.AnsiConsole.Profile.Capabilities.Interactive),
            "The test process must expose a redirected or non-interactive console.");

        var port = ClaimLoopbackPort();
        var builder = QylAppBuilder.Create(new QylAppOptions { RunnerPort = port });
        await using var app = builder.Build();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        var runTask = app.RunAsync(lifetime.Token);
        try
        {
            using var response = await WaitForResponseAsync(
                client,
                new Uri($"http://127.0.0.1:{port}/runner/resources"),
                lifetime.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(runTask.IsCompleted, "The host exited while its output was non-interactive.");
            Assert.Equal("[]", await response.Content.ReadAsStringAsync(lifetime.Token));
        }
        finally
        {
            await lifetime.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    private static int ClaimLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<HttpResponseMessage> WaitForResponseAsync(
        HttpClient client,
        Uri uri,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                return await client.GetAsync(uri, cancellationToken);
            }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(25, cancellationToken);
            }
        }
    }
}
