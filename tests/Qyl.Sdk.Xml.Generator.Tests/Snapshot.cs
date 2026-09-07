using System.Runtime.CompilerServices;

namespace Qyl.Sdk.Xml.Generator.Tests;

/// <summary>Compares generated text with a file under Snapshots/; <c>QYL_UPDATE_SNAPSHOTS=1</c> rewrites the file instead.</summary>
internal static class Snapshot
{
    public static void Matches(string actual, string name, [CallerFilePath] string callerPath = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(callerPath)!, "Snapshots", name);
        var normalized = actual.Replace("\r\n", "\n", StringComparison.Ordinal);

        if (Environment.GetEnvironmentVariable("QYL_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(path, normalized);
            return;
        }

        Assert.True(File.Exists(path), $"Snapshot {path} is missing; run once with QYL_UPDATE_SNAPSHOTS=1 and review the file.");
        var expected = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.True(
            string.Equals(expected, normalized, StringComparison.Ordinal),
            $"Generated source differs from {name}; review and rerun with QYL_UPDATE_SNAPSHOTS=1 to accept.");
    }
}
