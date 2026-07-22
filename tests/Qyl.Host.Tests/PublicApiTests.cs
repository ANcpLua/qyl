using System.Reflection;

namespace Qyl.Host.Tests;

public sealed class PublicApiTests
{
    [Fact]
    public void Resource_builder_exposes_only_its_opaque_name()
    {
        var property = Assert.Single(typeof(IQylResourceBuilder)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Equal(nameof(IQylResourceBuilder.Name), property.Name);
        Assert.Equal(typeof(string), property.PropertyType);

        var method = Assert.Single(typeof(IQylResourceBuilder)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly));
        Assert.Equal("get_Name", method.Name);
    }

    [Fact]
    public void Internal_runner_models_are_not_exported()
    {
        var exportedNames = typeof(QylApp).Assembly.ExportedTypes
            .Select(static type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Qyl.Host.QylResource", exportedNames);
        Assert.DoesNotContain("Qyl.Host.QylLaunchSpec", exportedNames);
        Assert.DoesNotContain("Qyl.Host.QylResourceState", exportedNames);
        Assert.DoesNotContain("Qyl.Host.QylConstants", exportedNames);
    }

    [Fact]
    public void Removed_no_op_self_reference_switch_cannot_return()
    {
        var method = typeof(QylSelfTelemetryBuilder).GetMethod(
            "RejectSelfReference",
            BindingFlags.Instance | BindingFlags.Public);

        Assert.Null(method);
    }

    [Fact]
    public void Built_app_does_not_expose_the_internal_service_provider()
    {
        Assert.Null(typeof(QylApp).GetProperty(
            "Services",
            BindingFlags.Instance | BindingFlags.Public));
    }
}
