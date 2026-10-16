using System.Globalization;

namespace Qyl.Cli.Tests;

[TestClass]
public sealed class BoundedStreamTests
{
    [TestMethod]
    public void Resource_state_bursts_conflate_to_a_full_snapshot_resync()
    {
        var resources = Enumerable.Range(0, 96)
            .Select(index => new QylResource
            {
                Name = $"resource-{index}",
                Kind = QylResourceKind.Command,
                Port = index + 1000,
                Launch = new QylLaunchSpec { Executable = "test" }
            })
            .ToArray();
        var registry = new QylResourceRegistry(resources, TimeProvider.System);
        using var subscription = registry.Subscribe();

        foreach (var resource in resources)
            registry.Publish(resource.Name, ResourceLifecycle.Ready, resource.Port);

        Assert.IsTrue(subscription.Events.TryRead(out _));
        Assert.IsFalse(subscription.Events.TryRead(out _));
        Assert.AreEqual(resources.Length, registry.Snapshot.Count);
        foreach (var state in registry.Snapshot.Values)
            Assert.AreEqual(ResourceLifecycle.Ready, state.Lifecycle);
    }

    [TestMethod]
    public void Slow_log_subscribers_retain_only_the_bounded_replay_window()
    {
        var logs = new QylLogStore();
        using var subscription = logs.Subscribe("worker");
        for (var index = 0; index < QylLogStore.SubscriberCapacity + 100; index++)
            logs.Append("worker", isError: false, index.ToString(CultureInfo.InvariantCulture));

        var retained = new List<QylLogLine>();
        while (subscription.Events.TryRead(out var line)) retained.Add(line);

        Assert.AreEqual(QylLogStore.SubscriberCapacity, retained.Count);
        Assert.AreEqual("100", retained[0].Line);
        Assert.AreEqual((QylLogStore.SubscriberCapacity + 99).ToString(CultureInfo.InvariantCulture), retained[^1].Line);
    }
}
