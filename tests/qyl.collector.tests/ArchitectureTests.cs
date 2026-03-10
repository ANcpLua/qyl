using NetArchTest.Rules;
using Qyl.Agents.Agents;
using Qyl.Contracts.Common;
using Qyl.Workflows;
using Xunit;

namespace Qyl.Collector.tests;

/// <summary>
///     Enforces the decomposed project boundaries established during the Loom migration.
///     See docs/plans/2026-03-10-qyl-to-qyl-loom-migration.md for rationale.
/// </summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void Agents_Should_Not_Depend_On_Loom_Or_Collector()
    {
        var result = Types.InAssembly(typeof(QylAgentBuilder).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Qyl.Loom", "Qyl.Collector")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact]
    public void Workflows_Should_Not_Depend_On_Loom_Or_Collector()
    {
        var result = Types.InAssembly(typeof(WorkflowServiceExtensions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Qyl.Loom", "Qyl.Collector")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Any_Project()
    {
        var result = Types.InAssembly(typeof(TimeConversions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Qyl.Collector",
                "Qyl.Agents",
                "Qyl.Workflows",
                "Qyl.Loom",
                "Qyl.Instrumentation",
                "Qyl.Mcp")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    [Fact]
    public void Mcp_Should_Not_Reference_Collector_Assembly()
    {
        var mcpAssembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(static a => a.GetName().Name == "qyl.mcp");

        if (mcpAssembly is null)
        {
            return; // MCP not loaded in this test run — skip
        }

        var result = Types.InAssembly(mcpAssembly)
            .ShouldNot()
            .HaveDependencyOn("Qyl.Collector")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatFailure(result));
    }

    private static string FormatFailure(NetArchTest.Rules.TestResult result)
    {
        var violators = result.FailingTypeNames ?? [];
        return $"Architecture violation: {string.Join(", ", violators)}";
    }
}
