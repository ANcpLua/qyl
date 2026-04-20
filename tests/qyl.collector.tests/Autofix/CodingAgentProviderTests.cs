namespace Qyl.Collector.Tests.Autofix;

using Qyl.Contracts.Loom;
using Xunit;

public sealed class CodingAgentProviderTests
{
    // =========================================================================
    // ToSlug
    // =========================================================================

    [Theory]
    [InlineData(CodingAgentProvider.Loom, "Loom")]
    [InlineData(CodingAgentProvider.Cursor, "cursor")]
    [InlineData(CodingAgentProvider.GithubCopilot, "github_copilot")]
    [InlineData(CodingAgentProvider.ClaudeCode, "claude_code")]
    public void ToSlug_KnownProvider_ReturnsExpectedSlug(CodingAgentProvider provider, string expected)
    {
        var actual = CodingAgentProviderNames.ToSlug(provider);

        actual.Should().Be(expected);
    }

    [Fact]
    public void ToSlug_UnknownProvider_Throws()
    {
        var act = static () => CodingAgentProviderNames.ToSlug((CodingAgentProvider)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // =========================================================================
    // NormalizeSlug — happy paths
    // =========================================================================

    [Theory]
    [InlineData("Loom", "Loom")]
    [InlineData("loom", "Loom")]
    [InlineData("LOOM", "Loom")]
    [InlineData("cursor", "cursor")]
    [InlineData("Cursor", "cursor")]
    [InlineData("CURSOR", "cursor")]
    [InlineData("github_copilot", "github_copilot")]
    [InlineData("GITHUB_COPILOT", "github_copilot")]
    [InlineData("GithubCopilot", "github_copilot")]
    [InlineData("github-copilot", "github_copilot")]
    [InlineData("claude_code", "claude_code")]
    [InlineData("CLAUDE_CODE", "claude_code")]
    [InlineData("ClaudeCode", "claude_code")]
    [InlineData("claude-code", "claude_code")]
    public void NormalizeSlug_KnownValues_ReturnCanonicalSlug(string input, string expected)
    {
        var actual = CodingAgentProviderNames.NormalizeSlug(input);

        actual.Should().Be(expected);
    }

    // =========================================================================
    // NormalizeSlug — null / empty / unknown fallback
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("totally-unknown-provider")]
    [InlineData("openai")]
    public void NormalizeSlug_UnknownOrEmpty_FallsBackToLoom(string? input)
    {
        var actual = CodingAgentProviderNames.NormalizeSlug(input);

        actual.Should().Be("Loom");
    }

    // =========================================================================
    // Round-trip: ToSlug → NormalizeSlug
    // =========================================================================

    [Theory]
    [InlineData(CodingAgentProvider.Loom)]
    [InlineData(CodingAgentProvider.Cursor)]
    [InlineData(CodingAgentProvider.GithubCopilot)]
    [InlineData(CodingAgentProvider.ClaudeCode)]
    public void RoundTrip_ToSlug_ThenNormalize_IsIdempotent(CodingAgentProvider provider)
    {
        var slug = CodingAgentProviderNames.ToSlug(provider);
        var normalized = CodingAgentProviderNames.NormalizeSlug(slug);

        normalized.Should().Be(slug);
    }

    // =========================================================================
    // TryParse
    // =========================================================================

    [Theory]
    [InlineData("Loom", CodingAgentProvider.Loom)]
    [InlineData("cursor", CodingAgentProvider.Cursor)]
    [InlineData("github_copilot", CodingAgentProvider.GithubCopilot)]
    [InlineData("claude_code", CodingAgentProvider.ClaudeCode)]
    public void TryParse_KnownSlug_ReturnsTrueWithCorrectProvider(string input, CodingAgentProvider expected)
    {
        var success = CodingAgentProviderNames.TryParse(input, out var provider);

        success.Should().BeTrue();
        provider.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown-provider")]
    public void TryParse_NullEmptyOrUnknown_ReturnsFalse(string? input)
    {
        var success = CodingAgentProviderNames.TryParse(input, out _);

        success.Should().BeFalse();
    }

    [Fact]
    public void TryParse_NullInput_OutParamDefaultsToLoom()
    {
        var success = CodingAgentProviderNames.TryParse(null, out var provider);

        success.Should().BeFalse();
        provider.Should().Be(CodingAgentProvider.Loom);
    }
}
