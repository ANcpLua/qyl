namespace qyl.collector.BuildFailures;

public sealed class BinlogParser
{
    public BuildFailureIngestRequest Parse(string binlogPath, string target, int exitCode)
    {
        try
        {
            var exists = File.Exists(binlogPath);
            var summary = exists
                ? $"Build failed for target '{target}'. Binlog captured at '{binlogPath}'."
                : "Build failed and binlog file was not found.";

            var propertyIssues = Array.Empty<string>();
            var envReads = Array.Empty<string>();

            return new BuildFailureIngestRequest(
                null,
                TimeProvider.System.GetUtcNow(),
                target,
                exitCode,
                binlogPath,
                summary,
                JsonSerializer.Serialize(propertyIssues),
                JsonSerializer.Serialize(envReads),
                null,
                null);
        }
        catch
        {
            return new BuildFailureIngestRequest(
                null,
                TimeProvider.System.GetUtcNow(),
                target,
                exitCode,
                binlogPath,
                "Failed to parse binlog",
                null,
                null,
                null,
                null);
        }
    }
}
