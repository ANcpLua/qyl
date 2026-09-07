using Qyl;

namespace Qyl.Api.Tests;

/// <summary>
/// The one thing about the contract revision this assembly can honestly assert: the attribute key.
/// </summary>
/// <remarks>
/// The value is not testable here and is deliberately not faked. It is written by MSBuild into the consumer's
/// own compilation (<c>QylSdkBuild.ContractRevision</c>, which this project does not have, because it is not
/// built by the SDK), and read back off a real span by <c>ApiSdkSessionScenario</c>, whose oracle spells the
/// <c>sha256:</c> format independently of the targets that produce it. A test here could only restate a literal
/// and match itself, which would stay green through exactly the regression the gate exists to catch.
///
/// The key is a different matter: it comes from the pinned semantic-convention package, so this pins what the
/// registry is expected to call it, and a rename upstream fails here instead of arriving silently.
/// </remarks>
public sealed class ContractAttributeTests
{
    [Fact]
    public void The_revision_attribute_is_the_registry_key() =>
        Assert.Equal("qyl.api.contract.revision", QylApiContract.RevisionAttributeName, StringComparer.Ordinal);
}
