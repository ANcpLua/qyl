namespace Qyl.Collector.Tests;

using System.Reflection;
using NetArchTest.Rules;
using Qyl.Contracts.Primitives;
using Xunit;
using TestResult = NetArchTest.Rules.TestResult;

/// <summary>
///     Enforces project dependency boundaries across the qyl platform.
///     v2 architecture: collector may depend on M.E.AI abstractions, but not
///     MAF runtime packages, GitHub Copilot SDK, or concrete provider SDKs.
/// </summary>
public sealed class ArchitectureTests
{
    [Fact]
    public void Server_Should_Not_Depend_On_Agent_Framework()
    {
        var result = Types.InAssembly(typeof(Program).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.Agents.AI",
                "Microsoft.Agents.AI.Hosting",
                "Microsoft.Agents.AI.Hosting.AGUI",
                "GitHub.Copilot.SDK")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailure(result));
    }

    [Fact]
    public void Contracts_Should_Not_Depend_On_Any_Project()
    {
        var result = Types.InAssembly(typeof(TimeConversions).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Qyl.Collector",
                "Qyl.Loom",
                "Qyl.Instrumentation",
                "Qyl.Mcp")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FormatFailure(result));
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

        result.IsSuccessful.Should().BeTrue(FormatFailure(result));
    }

    private static string FormatFailure(TestResult result)
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
