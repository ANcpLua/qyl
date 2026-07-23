using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void Runtime_types_are_not_exported_from_the_qyl_tool()
    {
        Assert.DoesNotContain(
            typeof(QylApp).Assembly.ExportedTypes,
            static type => type.Namespace?.StartsWith("Qyl.Cli.Runtime", StringComparison.Ordinal) is true);
    }
}
