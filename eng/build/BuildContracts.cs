using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Components;
using Serilog;

namespace Qyl.Build;

/// <summary>
/// The API contract in <c>contracts/</c>: TypeSpec compiled by the locked toolchain into the committed
/// OpenAPI, the C# contracts in <c>internal/qyl.contracts</c> and the MCP's TypeScript types and tool
/// schemas in <c>services/qyl.mcp/src/contracts/generated</c>.
/// </summary>
interface IContracts : IHasSourcePaths
{
    AbsolutePath ContractsDirectory => RootDirectory / "contracts";
    AbsolutePath OpenApiContractFile => ContractsDirectory / "openapi" / "qyl.openapi.json";

    /// <summary>Every directory the contract compile writes.</summary>
    AbsolutePath[] GeneratedContractDirectories =>
    [
        ContractsDirectory / "openapi",
        InternalDirectory / "qyl.contracts",
        ServicesDirectory / "qyl.mcp" / "src" / "contracts" / "generated"
    ];

    Target Contracts => d => d
        .Description("Regenerate the OpenAPI, the C# contracts and the MCP's TypeScript types and tool schemas from contracts/")
        .Before<ICompile>(static x => x.Compile)
        .Before<IMcp>(static x => x.McpBuild)
        .Executes(() => CompileContracts(ContractsDirectory));

    Target VerifyContractsCurrent => d => d
        .Description("Regenerate the contracts and fail when any generated file differs from the one in the tree")
        .Before<ICompile>(static x => x.Compile)
        .Before<IMcp>(static x => x.McpBuild)
        .Executes(() =>
        {
            var before = Snapshot(GeneratedContractDirectories);
            CompileContracts(ContractsDirectory);
            var after = Snapshot(GeneratedContractDirectories);

            var changed = before.Keys.Union(after.Keys, StringComparer.Ordinal)
                .Where(path => !before.TryGetValue(path, out var old) || !after.TryGetValue(path, out var current) || !old.AsSpan().SequenceEqual(current))
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (changed.Length is 0)
            {
                Log.Information("The generated contracts match contracts/");
                return;
            }

            foreach (var path in changed)
                Log.Error("  {State} {File}", !before.ContainsKey(path) ? "added" : !after.ContainsKey(path) ? "removed" : "changed", RootDirectory.GetRelativePathTo(path));
            throw new InvalidOperationException(
                "The generated contracts are stale: run ./eng/build.sh Contracts and commit the result with the TypeSpec change.");
        });

    private static void CompileContracts(AbsolutePath directory)
    {
        ProcessTasks.StartProcess("bun", "install --frozen-lockfile", directory, logOutput: true).AssertZeroExitCode();
        ProcessTasks.StartProcess("bun", "run compile", directory, logOutput: true).AssertZeroExitCode();
    }

    private static Dictionary<string, byte[]> Snapshot(IEnumerable<AbsolutePath> directories) =>
        directories
            .Where(static directory => directory.DirectoryExists())
            .SelectMany(static directory => directory.GlobFiles("**/*"))
            .ToDictionary(static file => file.ToString(), static file => File.ReadAllBytes(file), StringComparer.Ordinal);
}
