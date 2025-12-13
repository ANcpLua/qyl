using System;
using System.IO;
using System.Linq;
using System.Text;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Serilog;

namespace Components;

internal interface IChangelog : ICompile
{
    [PathVariable] Tool Git => TryGetValue(() => Git)!;

    AbsolutePath ChangelogDirectory => ArtifactsDirectory / "changelog";

    Target Changelog => d => d
        .Description("Generate changelog from git history")
        .DependsOn<ICompile>(x => x.Compile)
        .Produces(ChangelogDirectory / "*.md")
        .Executes(() =>
        {
            ChangelogDirectory.CreateDirectory();

            var outputFile = ChangelogDirectory / "CHANGELOG_FROM_LAST_COMMIT.md";
            StringBuilder sb = new();

            sb.AppendLine("# Changelog")
                .AppendLine()
                .AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                .AppendLine();

            var branch = RunGitSafe("rev-parse --abbrev-ref HEAD").Trim();
            var commit = RunGitSafe("rev-parse --short HEAD").Trim();
            var commitFull = RunGitSafe("rev-parse HEAD").Trim();

            sb.AppendLine("## Current State")
                .AppendLine()
                .AppendLine($"- **Branch:** `{branch}`")
                .AppendLine($"- **Commit:** `{commit}` ({commitFull})");

            if (GitVersion is { } gv)
                sb.AppendLine($"- **Version:** `{gv.FullSemVer}`")
                    .AppendLine($"- **Informational Version:** `{gv.InformationalVersion}`");

            sb.AppendLine();

            sb.AppendLine("## Recent Commits").AppendLine();
            var commitLog = RunGitSafe("log --oneline -10 --pretty=format:\"- `%h` %s (%an, %ar)\"");
            sb.AppendLine(commitLog is { Length: > 0 } ? commitLog : "_No commits found_").AppendLine();

            sb.AppendLine("## Changed Files (HEAD~1..HEAD)").AppendLine();
            var changedFiles = RunGitSafe("diff --name-status HEAD~1..HEAD");

            if (changedFiles is { Length: > 0 })
            {
                sb.AppendLine("| Status | File |")
                    .AppendLine("|--------|------|");

                foreach (var line in changedFiles.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = line.Split('\t', 2);
                    if (parts.Length is not 2) continue;
                    var status = parts[0] switch
                    {
                        "A" => "Added",
                        "M" => "Modified",
                        "D" => "Deleted",
                        "R" => "Renamed",
                        "C" => "Copied",
                        _ => parts[0]
                    };
                    sb.AppendLine($"| {status} | `{parts[1]}` |");
                }
            }
            else
                sb.AppendLine("_No changes detected or this is the first commit_");

            sb.AppendLine();

            sb.AppendLine("## Uncommitted Changes").AppendLine();
            var gitStatus = RunGitSafe("status --porcelain");
            sb.AppendLine(gitStatus is { Length: > 0 }
                ? $"```\n{gitStatus}\n```"
                : "_Working directory is clean_");
            sb.AppendLine();

            sb.AppendLine("## Recent Tags").AppendLine();
            var tags = RunGitSafe("tag --sort=-creatordate");

            if (tags is { Length: > 0 })
            {
                var tagList = tags.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5);
                foreach (var tag in tagList)
                    sb.AppendLine($"- `{tag}`");
            }
            else
                sb.AppendLine("_No tags found_");

            File.WriteAllText(outputFile, sb.ToString());
            Log.Information("Changelog written to: {Path}", outputFile);
        });

    private string RunGitSafe(string arguments)
    {
        try
        {
            var output = Git(
                arguments,
                RootDirectory,
                logOutput: false,
                logInvocation: false);

            return string.Join('\n', output.Select(o => o.Text));
        }
        catch
        {
            return string.Empty;
        }
    }
}