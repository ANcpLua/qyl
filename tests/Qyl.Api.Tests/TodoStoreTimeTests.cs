using Microsoft.Extensions.Time.Testing;
using Qyl.Sample;

namespace Qyl.Api.Tests;

[TestClass]
public sealed class TodoStoreTimeTests
{
    [TestMethod]
    [DataRow(0, 17, 18)]
    [DataRow(2, 18, 18)]
    [DataRow(-8, 17, 17)]
    public void Seeded_due_dates_use_the_local_calendar_at_store_creation(int offsetHours, int initialDay, int advancedDay)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 17, 23, 30, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("test", TimeSpan.FromHours(offsetHours), "test", "test"));
        var store = new TodoStore(clock);
        var today = new DateOnly(2026, 10, initialDay);

        Assert.AreEqual(today, store.Find(2)!.DueBy);
        Assert.AreEqual(today.AddDays(1), store.Find(3)!.DueBy);
        Assert.AreEqual(today.AddDays(2), store.Find(5)!.DueBy);

        clock.Advance(TimeSpan.FromHours(1));

        Assert.AreEqual(today, store.Find(2)!.DueBy, "Existing due dates are seeded data, not live clock projections.");
        Assert.AreEqual(new DateOnly(2026, 10, advancedDay), new TodoStore(clock).Find(2)!.DueBy);
    }
}
