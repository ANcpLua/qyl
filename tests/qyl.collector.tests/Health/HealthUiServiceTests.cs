using Qyl.Collector.Health;
using Qyl.Models;
using Xunit;

namespace Qyl.Collector.Tests.Health;

/// <summary>
///     Unit tests for <see cref="HealthUiService.DetermineOverallStatus" />.
/// </summary>
/// <summary>
///     Unit tests for <see cref="HealthUiService.DetermineOverallStatus" />
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
            new() { Name = "c", Status = HealthStatus.Unhealthy }
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        result.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void DetermineOverallStatus_ReturnsDegraded_WhenNoneUnhealthyButSomeDegraded()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Healthy },
            new() { Name = "b", Status = HealthStatus.Degraded },
            new() { Name = "c", Status = HealthStatus.Healthy }
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        result.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void DetermineOverallStatus_ReturnsHealthy_WhenAllHealthy()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Healthy },
            new() { Name = "b", Status = HealthStatus.Healthy },
            new() { Name = "c", Status = HealthStatus.Healthy }
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        result.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void DetermineOverallStatus_UnhealthyTakesPrecedenceOverDegraded()
    {
        ComponentHealth[] components =
        [
            new() { Name = "a", Status = HealthStatus.Degraded },
            new() { Name = "b", Status = HealthStatus.Unhealthy },
            new() { Name = "c", Status = HealthStatus.Degraded }
        ];

        var result = HealthUiService.DetermineOverallStatus(components);

        result.Should().Be(HealthStatus.Unhealthy);
    }
}
