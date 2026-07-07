// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
// qyl adaptation: the expected SDK version comes from global.json, the repo's
// single source of truth for the toolchain, instead of a setup-dotnet action file.

using System.Text.Json;

namespace SdkVersionAnalyzer;

internal static class GlobalJsonAnalyzer
{
    public static DotnetSdkVersion? GetExpectedSdkVersion(string root)
    {
        var globalJsonPath = Path.Combine(root, "global.json");
        if (!File.Exists(globalJsonPath))
        {
            return null;
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(globalJsonPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });

        if (!document.RootElement.TryGetProperty("sdk", out var sdk) ||
            !sdk.TryGetProperty("version", out var versionElement))
        {
            return null;
        }

        var version = versionElement.GetString();
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        return new DotnetSdkVersion(
            VersionComparer.IsNet8Version(version) ? version : null,
            VersionComparer.IsNet9Version(version) ? version : null,
            VersionComparer.IsNet10Version(version) ? version : null);
    }
}
