using Qyl.Collector.Hosting;

namespace Qyl.Collector.Tests;

[TestClass]
public sealed class ContractIdentifiersTests
{
    [TestMethod]
    public void IsTraceId_ThirtyTwoLowerHexDigits_IsTrue()
    {
        Assert.IsTrue(ContractIdentifiers.IsTraceId("0123456789abcdef0123456789abcdef"));
    }

    [TestMethod]
    [DataRow("0123456789ABCDEF0123456789abcdef")]
    [DataRow("0123456789abcdeg0123456789abcdef")]
    [DataRow("0123456789abcdef0123456789abcde")]
    [DataRow("")]
    [DataRow(null)]
    public void IsTraceId_NotThirtyTwoLowerHexDigits_IsFalse(string? value)
    {
        Assert.IsFalse(ContractIdentifiers.IsTraceId(value));
    }

    [TestMethod]
    public void IsSpanId_SixteenLowerHexDigits_IsTrue()
    {
        Assert.IsTrue(ContractIdentifiers.IsSpanId("0123456789abcdef"));
    }

    [TestMethod]
    [DataRow("0123456789ABCDEF")]
    [DataRow("0123456789abcde/")]
    [DataRow("0123456789abcdef0")]
    public void IsSpanId_NotSixteenLowerHexDigits_IsFalse(string value)
    {
        Assert.IsFalse(ContractIdentifiers.IsSpanId(value));
    }
}
