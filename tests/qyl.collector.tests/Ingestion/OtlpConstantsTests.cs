using Qyl.Collector.Auth;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests.Ingestion;

public sealed class OtlpConstantsTests
{
    [Theory]
    [InlineData("/v1/traces", true)]
    [InlineData("/v1/logs", true)]
    [InlineData("/v1/profiles", true)]
    [InlineData("/v1/metrics", false)]
    [InlineData("/healthz", false)]
    [InlineData("", false)]
    public void IsOtlpPath_RecognisesMappedOtlpEndpoints(string path, bool expected) =>
        OtlpConstants.IsOtlpPath(path).Should().Be(expected);

    [Theory]
    [InlineData("/v1/traces", true)]
    [InlineData("/v1/logs", true)]
    [InlineData("/v1/profiles", true)]
    [InlineData("/v1/metrics", false)]
    public void TokenAuthDefaults_BypassMatchesMappedOtlpPaths(string path, bool isBypassed) =>
        new TokenAuthOptions().ExcludedPaths.Contains(path).Should().Be(isBypassed);
}
