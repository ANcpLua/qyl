namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ContractDateTimeTests
{
    [TestMethod]
    [DataRow("0000-02-29T00:00:00Z")]
    [DataRow("2000-02-29T00:00:00Z")]
    [DataRow("2024-02-29T00:00:00Z")]
    [DataRow("2026-01-31T00:00:00Z")]
    [DataRow("2026-12-31T00:00:00Z")]
    public void IsValid_LastDayOfItsMonth_IsTrue(string text)
    {
        Assert.IsTrue(ContractDateTime.IsValid(text));
    }

    [TestMethod]
    [DataRow("0100-02-29T00:00:00Z")]
    [DataRow("1900-02-29T00:00:00Z")]
    [DataRow("2026-02-29T00:00:00Z")]
    [DataRow("2026-04-31T00:00:00Z")]
    [DataRow("2026-00-01T00:00:00Z")]
    [DataRow("2026-13-01T00:00:00Z")]
    [DataRow("2026-01-00T00:00:00Z")]
    public void IsValid_DayOrMonthOutsideTheCalendar_IsFalse(string text)
    {
        Assert.IsFalse(ContractDateTime.IsValid(text));
    }
}
