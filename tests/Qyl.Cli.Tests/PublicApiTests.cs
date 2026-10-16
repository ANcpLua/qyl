using Qyl.Cli.Runtime;

namespace Qyl.Cli.Tests;

[TestClass]
public sealed class PublicApiTests
{
    [TestMethod]
    public void Runtime_types_are_not_exported_from_the_qyl_tool()
    {
        var runtimeTypes = typeof(QylApp).Assembly.ExportedTypes.Where(
            static type => type.Namespace?.StartsWith("Qyl.Cli.Runtime", StringComparison.Ordinal) is true);
        Assert.IsEmpty(runtimeTypes);
    }
}
