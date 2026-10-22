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
}
