// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
// qyl adaptation: discovers `Dockerfile` and `*.dockerfile` anywhere in the repo,
// inspects every FROM stage (multi-stage builds), and treats channel-only tags
// (e.g. sdk:10.0@sha256:...) as unpinned — only fully pinned x.y.z tags are checked.

using System.Text.RegularExpressions;
using Valleysoft.DockerfileModel;

namespace SdkVersionAnalyzer;

internal static partial class DockerfileAnalyzer
{
    public static bool VerifyVersions(string root, DotnetSdkVersion expectedDotnetSdkVersion)
    {
        return FileAnalyzer.VerifyMultiple(GetDockerfiles(root), VerifySdkVersions, expectedDotnetSdkVersion);
    }

    public static void ModifyVersions(string root, DotnetSdkVersion requestedDotnetSdkVersion)
    {
        FileAnalyzer.ModifyMultiple(GetDockerfiles(root), ModifySdkVersions, requestedDotnetSdkVersion);
    }

    [GeneratedRegex(@"-v (\d+\.\d+\.\d{3})\s", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex VersionRegex();

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex PinnedVersionRegex();

    private static string ModifySdkVersions(string content, DotnetSdkVersion requestedDotnetSdkVersion)
    {
        var dockerfile = Dockerfile.Parse(content);
        var runInstruction = GetDotnetInstallingInstruction(dockerfile);

        if (runInstruction?.Command is not null)
        {
            runInstruction.Command = GetModifiedInstallCommand(runInstruction.Command, requestedDotnetSdkVersion);
        }

        foreach (var fromInstruction in dockerfile.Items.OfType<FromInstruction>())
        {
            var imageName = ImageName.Parse(fromInstruction.ImageName);
            if (!IsDotnetSdkImage(imageName) || GetPinnedSdkVersion(imageName) is not { } sdkVersion)
            {
                continue;
            }

            if (imageName.Digest is not null)
            {
                Console.WriteLine("Digest-pinned SDK image left unchanged (digest updates are Renovate's job).");
                continue;
            }

            var suffix = GetTagSuffix(imageName);
            var modifiedTag = suffix is null
                ? GetNewVersion(sdkVersion, requestedDotnetSdkVersion)
                : $"{GetNewVersion(sdkVersion, requestedDotnetSdkVersion)}-{suffix}";
            fromInstruction.ImageName = ImageName.FormatImageName(imageName.Repository, imageName.Registry, modifiedTag, null);
        }

        return dockerfile.ToString();
    }

    private static bool VerifySdkVersions(string content, DotnetSdkVersion expectedDotnetSdkVersion)
    {
        string? net8SdkVersion = null;
        string? net9SdkVersion = null;
        string? net10SdkVersion = null;

        var dockerfile = Dockerfile.Parse(content);
        var instruction = GetDotnetInstallingInstruction(dockerfile);

        // Extract versions from dotnet-install.sh invocations like:
        //     && ./dotnet-install.sh -v 10.0.301 --install-dir /usr/share/dotnet --no-path \
        if (instruction is not null)
        {
            var matchCollection = VersionRegex().Matches(instruction.ToString());
            foreach (Match match in matchCollection)
            {
                var extractedSdkVersion = match.Groups[1].Value;
                if (VersionComparer.IsNet8Version(extractedSdkVersion))
                {
                    net8SdkVersion = extractedSdkVersion;
                }
                else if (VersionComparer.IsNet9Version(extractedSdkVersion))
                {
                    net9SdkVersion = extractedSdkVersion;
                }
                else if (VersionComparer.IsNet10Version(extractedSdkVersion))
                {
                    net10SdkVersion = extractedSdkVersion;
                }
            }
        }

        // Extract pinned SDK versions from base image tags across all stages,
        // e.g. FROM mcr.microsoft.com/dotnet/sdk:10.0.301-alpine3.22
        foreach (var fromInstruction in dockerfile.Items.OfType<FromInstruction>())
        {
            var imageName = ImageName.Parse(fromInstruction.ImageName);
            if (!IsDotnetSdkImage(imageName) || GetPinnedSdkVersion(imageName) is not { } sdkVersion)
            {
                continue;
            }

            if (VersionComparer.IsNet8Version(sdkVersion))
            {
                net8SdkVersion = sdkVersion;
            }
            else if (VersionComparer.IsNet9Version(sdkVersion))
            {
                net9SdkVersion = sdkVersion;
            }
            else if (VersionComparer.IsNet10Version(sdkVersion))
            {
                net10SdkVersion = sdkVersion;
            }
        }

        return VersionComparer.CompareVersions(expectedDotnetSdkVersion, net8SdkVersion, net9SdkVersion, net10SdkVersion);
    }

    private static string? GetPinnedSdkVersion(ImageName imageName)
    {
        // Tag may be absent (digest-only), a channel like "10.0", or pinned like
        // "10.0.301-alpine3.22". Only a fully pinned x.y.z tag participates in drift checks.
        var version = imageName.Tag?.Split('-', 2)[0];
        return version is not null && PinnedVersionRegex().IsMatch(version) ? version : null;
    }

    private static string? GetTagSuffix(ImageName imageName)
    {
        var parts = imageName.Tag!.Split('-', 2);
        return parts.Length == 2 ? parts[1] : null;
    }

    private static bool IsDotnetSdkImage(ImageName imageName)
    {
        return imageName is { Registry: "mcr.microsoft.com", Repository: "dotnet/sdk" };
    }

    private static Command GetModifiedInstallCommand(Command command, DotnetSdkVersion requestedDotnetSdkVersion)
    {
        var newCommandText = VersionRegex().Replace(command.ToString(), match => $"-v {GetNewVersion(match.Groups[1].Value, requestedDotnetSdkVersion)} ");
        return command.CommandType == CommandType.ShellForm ? ShellFormCommand.Parse(newCommandText) : ExecFormCommand.Parse(newCommandText);
    }

    private static string GetNewVersion(string oldVersion, DotnetSdkVersion requestedDotnetSdkVersion)
    {
        if (VersionComparer.IsNet8Version(oldVersion) && requestedDotnetSdkVersion.Net8SdkVersion is not null)
        {
            return requestedDotnetSdkVersion.Net8SdkVersion;
        }

        if (VersionComparer.IsNet9Version(oldVersion) && requestedDotnetSdkVersion.Net9SdkVersion is not null)
        {
            return requestedDotnetSdkVersion.Net9SdkVersion;
        }

        if (VersionComparer.IsNet10Version(oldVersion) && requestedDotnetSdkVersion.Net10SdkVersion is not null)
        {
            return requestedDotnetSdkVersion.Net10SdkVersion;
        }

        return oldVersion;
    }

    private static string[] GetDockerfiles(string root)
    {
        string[] excludedSegments =
        [
            $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}Artifacts{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
            $"{Path.DirectorySeparatorChar}reference{Path.DirectorySeparatorChar}",
        ];

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(file =>
            {
                var fileName = Path.GetFileName(file);
                return fileName == "Dockerfile" || fileName.EndsWith(".dockerfile", StringComparison.Ordinal);
            })
            .Where(file => !excludedSegments.Any(segment => file.Contains(segment, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static RunInstruction? GetDotnetInstallingInstruction(Dockerfile dockerfile)
    {
        return dockerfile.Items.OfType<RunInstruction>().SingleOrDefault(i => i.ToString().Contains("./dotnet-install.sh", StringComparison.Ordinal));
    }
}
