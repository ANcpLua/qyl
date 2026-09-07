using Qyl;

namespace Qyl.Api.Tests;

/// <summary>The admission rule for a value an agent supplies, as an allow-list rather than a list of known abuses.</summary>
public sealed class SessionIdTests
{
    [Theory]
    [InlineData("agent-42")]
    [InlineData("a")]
    [InlineData("A.b_c~d-1")]
    [InlineData("0123456789")]
    public void Unreserved_characters_are_a_session_id(string candidate) =>
        Assert.True(QylApiContract.IsSessionId(candidate));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("..")]
    [InlineData("../../admin")]
    [InlineData("%")]
    [InlineData("%2e%2e%2f")]
    [InlineData("agent 1")]
    [InlineData(" agent-1")]
    [InlineData("agent-1 ")]
    [InlineData("agent\n1")]
    [InlineData("agent\t1")]
    // The Unicode line, paragraph and bidi separators: two different ids that print identically in a session list.
    [InlineData("a\u2028b")]
    [InlineData("a\u2029b")]
    [InlineData("a\u200Eb")]
    [InlineData("agent?1")]
    [InlineData("agent#1")]
    [InlineData("agent:1")]
    public void Anything_else_is_not(string? candidate) => Assert.False(QylApiContract.IsSessionId(candidate));

    [Fact]
    public void The_bound_is_the_documented_one()
    {
        Assert.True(QylApiContract.IsSessionId(new string('a', QylApiContract.MaxSessionIdLength)));
        Assert.False(QylApiContract.IsSessionId(new string('a', QylApiContract.MaxSessionIdLength + 1)));
    }
}
