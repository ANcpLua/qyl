using Qyl.Orchestration;

namespace Qyl.Cli.Tests;

[TestClass]
public sealed class QylApplicationTests(TestContext testContext)
{
    [TestMethod]
    public async Task DisposeAsync_CalledTwice_CompletesBothTimes()
    {
        var application = new QylApplicationBuilder().Build();
        await application.DisposeAsync();

        var second = application.DisposeAsync().AsTask();
        await second;

        Assert.AreEqual(TaskStatus.RanToCompletion, second.Status);
    }

    [TestMethod]
    public async Task StartAsync_AfterDispose_ThrowsObjectDisposedForTheApplication()
    {
        var application = new QylApplicationBuilder().Build();
        await application.DisposeAsync();

        var error = await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => application.StartAsync(testContext.CancellationToken));

        Assert.AreEqual(typeof(QylApplication).FullName, error.ObjectName);
    }

    [TestMethod]
    [Timeout(60_000)]
    public async Task StartAsync_QueuedBehindDispose_Completes()
    {
        const int queuedStarts = 1000;
        var builder = new QylApplicationBuilder();
        builder.AddExecutable("never-ready", "sleep", ["60"]).WithHealthCheck();
        var application = builder.Build();
        using var firstStartCancellation = CancellationTokenSource.CreateLinkedTokenSource(testContext.CancellationToken);
        var first = application.StartAsync(firstStartCancellation.Token);
        var disposal = application.DisposeAsync().AsTask();
        var queued = Enumerable.Range(0, queuedStarts).Select(_ => application.StartAsync(testContext.CancellationToken)).ToArray();
        Task[] calls = [first, disposal, .. queued];

        await firstStartCancellation.CancelAsync();
        await Task.WhenAll(calls)
            .ContinueWith(static _ => { }, TaskScheduler.Default)
            .WaitAsync(TimeSpan.FromSeconds(30), TimeProvider.System, testContext.CancellationToken);

        Assert.IsEmpty(calls.Where(static call => !call.IsCompleted));
    }
}
