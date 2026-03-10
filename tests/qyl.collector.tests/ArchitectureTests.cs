using System.Reflection;
using NetArchTest.Rules;
using Qyl.Agents.Agents;
using Qyl.Common;
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
        var mcpAssembly = LoadMcpAssembly();

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

    private static Assembly? LoadMcpAssembly()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
            return null;

        var assemblyPath = Path.Combine(
            repoRoot.FullName,
            "src",
            "qyl.mcp",
            "bin",
            "Debug",
            "net10.0",
            "qyl.mcp.dll");

        return File.Exists(assemblyPath)
            ? Assembly.LoadFrom(assemblyPath)
            : null;
    }

    private static DirectoryInfo? FindRepoRoot()
    {
        for (DirectoryInfo? current = new(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "qyl.slnx")))
                return current;
        }

        return null;
    }
}
