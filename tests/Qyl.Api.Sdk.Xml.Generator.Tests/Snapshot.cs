namespace Qyl.Api.Sdk.Xml.Generator.Tests;

/// <summary>Compares generated text with a file under Snapshots/; <c>QYL_UPDATE_SNAPSHOTS=1</c> rewrites the file instead.</summary>
internal static class Snapshot
{
    /// <summary>
    /// Found from the test binary upwards, not through [CallerFilePath]: a server build (Fallout under
    /// GitHub Actions) sets ContinuousIntegrationBuild, which maps compiler paths to /_/.
    /// </summary>
    private static readonly string SnapshotDirectory = Locate();

    public static void Matches(string actual, string name)
    {
        var path = Path.Combine(SnapshotDirectory, name);
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

    private static string Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "qyl.slnx")))
                return Path.Combine(directory.FullName, "tests", "Qyl.Api.Sdk.Xml.Generator.Tests", "Snapshots");
        }

        throw new InvalidOperationException($"No qyl.slnx above {AppContext.BaseDirectory}.");
    }
}
