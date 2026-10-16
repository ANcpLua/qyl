namespace Qyl.Api.Tests;

/// <summary>The admission rule for a value an agent supplies, as an allow-list rather than a list of known abuses.</summary>
[TestClass]
public sealed class SessionIdTests
{
    [TestMethod]
    [DataRow("agent-42")]
    [DataRow("a")]
    [DataRow("A.b_c~d-1")]
    [DataRow("0123456789")]
    public void Unreserved_characters_are_a_session_id(string candidate) =>
        Assert.IsTrue(QylApiContract.IsSessionId(candidate));

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("/")]
    [DataRow("..")]
    [DataRow("../../admin")]
    [DataRow("%")]
    [DataRow("%2e%2e%2f")]
    [DataRow("agent 1")]
    [DataRow(" agent-1")]
    [DataRow("agent-1 ")]
    [DataRow("agent\n1")]
    [DataRow("agent\t1")]
    // The Unicode line, paragraph and bidi separators: two different ids that print identically in a session list.
    [DataRow("a\u2028b")]
    [DataRow("a\u2029b")]
    [DataRow("a\u200Eb")]
    [DataRow("agent?1")]
    [DataRow("agent#1")]
    [DataRow("agent:1")]
    public void Anything_else_is_not(string? candidate) => Assert.IsFalse(QylApiContract.IsSessionId(candidate));

    [TestMethod]
    public void The_bound_is_the_documented_one()
    {
        Assert.IsTrue(QylApiContract.IsSessionId(new string('a', QylApiContract.MaxSessionIdLength)));
        Assert.IsFalse(QylApiContract.IsSessionId(new string('a', QylApiContract.MaxSessionIdLength + 1)));
    }
}
