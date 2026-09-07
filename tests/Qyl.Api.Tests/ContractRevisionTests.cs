using System.Text.RegularExpressions;
using Qyl;

namespace Qyl.Api.Tests;

/// <summary>
/// The value format of <c>qyl.api.contract.revision</c>. The registry defines it as
/// "SHA-256 of the committed OpenAPI document the API was built from, as <c>sha256:&lt;hex&gt;</c>", and the
/// collector reports its own revision the same way. Nothing in this assembly produces the value —
/// <c>Qyl.Sdk.Api.targets</c> writes it and the <c>ApiSdkSessionScenario</c> gate reads it back off a real
/// span — which is exactly why the shape is pinned here: those two spell it independently, and this is the
/// one place that says what they must both mean.
/// </summary>
public sealed class ContractRevisionTests
{
    /// <summary>A well-formed value in the registry's format.</summary>
    private const string WellFormed =
        "sha256:8b68fbf249d8a63af54e8a02c9aab1e37bdb4b51564fd465eec0c99f35aaf5a8";

    private static readonly Regex s_value = new(
        @"^sha256:[0-9a-f]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(1));

    [Fact]
    public void A_revision_is_the_algorithm_and_the_digest()
    {
        Assert.StartsWith(QylApiContract.RevisionAlgorithmPrefix, WellFormed, StringComparison.Ordinal);
        Assert.Matches(s_value, WellFormed);
    }

    [Theory]
    // A bare digest is what the first cut of the fusion emitted; the registry says it is not the value.
    [InlineData("8b68fbf249d8a63af54e8a02c9aab1e37bdb4b51564fd465eec0c99f35aaf5a8")]
    [InlineData("SHA256:8b68fbf249d8a63af54e8a02c9aab1e37bdb4b51564fd465eec0c99f35aaf5a8")]
    [InlineData("sha256:8B68FBF249D8A63AF54E8A02C9AAB1E37BDB4B51564FD465EEC0C99F35AAF5A8")]
    [InlineData("sha256:8b68fbf2")]
    [InlineData("sha256:")]
    [InlineData("")]
    public void Anything_else_is_not_a_revision(string candidate) => Assert.DoesNotMatch(s_value, candidate);

    [Fact]
    public void The_prefix_is_the_algorithm_and_nothing_more() =>
        Assert.Equal("sha256:", QylApiContract.RevisionAlgorithmPrefix, StringComparer.Ordinal);

    [Fact]
    public void The_attribute_name_is_the_registry_key() =>
        Assert.Equal("qyl.api.contract.revision", QylApiContract.RevisionAttributeName, StringComparer.Ordinal);
}
