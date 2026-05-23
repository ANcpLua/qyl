using Qyl.Collector.Auth;
using Qyl.Collector.Ingestion;

namespace Qyl.Collector.Tests.Ingestion;

public sealed class OtlpConstantsTests
{
    [Theory]
    [InlineData("/v1/traces")]
    [InlineData("/v1/logs")]
    [InlineData("/v1/profiles")]
    public void IsOtlpPath_ReturnsTrue_ForMappedOtlpEndpoints(string path)
    {
        OtlpConstants.IsOtlpPath(path).Should().BeTrue();
    }

    [Fact]
    public void IsOtlpPath_ReturnsFalse_ForUnmappedMetricsEndpoint()
    {
        OtlpConstants.IsOtlpPath("/v1/metrics").Should().BeFalse();
    }

    [Fact]
    public void TokenAuthDefaults_DoNotBypassUnmappedMetricsEndpoint()
    {
        var options = new TokenAuthOptions();

        options.ExcludedPaths.Should().NotContain("/v1/metrics");
    }

    [Fact]
    public void TokenAuthDefaults_BypassMappedProfilesEndpoint()
    {
        var options = new TokenAuthOptions();

        options.ExcludedPaths.Should().Contain("/v1/profiles");
    }
}
