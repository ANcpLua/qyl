using qyl.collector.Health;
using Qyl.Models;
using Xunit;

namespace qyl.collector.tests.Health;

/// <summary>
///     Unit tests for <see cref="HealthUiService.DetermineOverallStatus" />.
/// </summary>


/// <summary>
/// Unit tests for <see cref="HealthUiService.DetermineOverallStatus" />
/// </summary>
public sealed class HealthUiServiceTests
{
    [Fact]
    public void DetermineOverallStatus_ReturnsUnhealthy_WhenAnyUnhealthy()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Healthy },
            new() { Name = "b", Status = HealthStatus.Healthy },
            new() { Name = "c", Status = HealthStatus.Unhealthy },
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        Assert.Equal(HealthStatus.Unhealthy, result);
    }

    [Fact]
    public void DetermineOverallStatus_ReturnsDegraded_WhenNoneUnhealthyButSomeDegraded()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Healthy },
            new() { Name = "b", Status = HealthStatus.Degraded },
            new() { Name = "c", Status = HealthStatus.Healthy },
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        Assert.Equal(HealthStatus.Degraded, result);
    }

    [Fact]
    public void DetermineOverallStatus_ReturnsHealthy_WhenAllHealthy()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Healthy },
            new() { Name = "b", Status = HealthStatus.Healthy },
            new() { Name = "c", Status = HealthStatus.Healthy },
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        Assert.Equal(HealthStatus.Healthy, result);
    }

    [Fact]
    public void DetermineOverallStatus_UnhealthyTakesPrecedenceOverDegraded()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Degraded },
            new() { Name = "b", Status = HealthStatus.Unhealthy },
            new() { Name = "c", Status = HealthStatus.Degraded },
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        Assert.Equal(HealthStatus.Unhealthy, result);
    }
}
