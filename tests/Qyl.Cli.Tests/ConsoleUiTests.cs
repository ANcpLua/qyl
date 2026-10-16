using System.Net;
using System.Net.Sockets;
using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[DoNotParallelize]
[TestClass]
public sealed class ConsoleUiTests(TestContext testContext)
{

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, false)]
    public void Redirected_or_noninteractive_output_selects_the_headless_ui(
        bool outputRedirected,
        bool interactive)
    {
        Assert.IsTrue(QylConsoleUi.ShouldUseHeadlessUi(outputRedirected, interactive));
    }

    [TestMethod]
    public async Task Headless_in_process_host_stays_alive_and_serves_the_runner_api()
    {
        Assert.IsTrue(
            QylConsoleUi.ShouldUseHeadlessUi(
                Console.IsOutputRedirected,
                Spectre.Console.AnsiConsole.Profile.Capabilities.Interactive),
            "The test process must expose a redirected or non-interactive console.");

        var port = ClaimLoopbackPort();
        var builder = QylAppBuilder.Create(new QylAppOptions { RunnerPort = port });
        await using var app = builder.Build();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(
            testContext.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(15));
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2);

        var runTask = app.RunAsync(lifetime.Token);
        try
        {
            using var response = await WaitForResponseAsync(
                client,
                new Uri($"http://127.0.0.1:{port}/runner/resources"),
                lifetime.Token);

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsFalse(runTask.IsCompleted, "The host exited while its output was non-interactive.");
            Assert.AreEqual("[]", await response.Content.ReadAsStringAsync(lifetime.Token));
        }
        finally
        {
            await lifetime.CancelAsync();
            await runTask.WaitAsync(TimeSpan.FromSeconds(5), testContext.CancellationToken);
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
