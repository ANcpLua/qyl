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
                Id: null,
                Timestamp: TimeProvider.System.GetUtcNow(),
                Target: target,
                ExitCode: exitCode,
                BinlogPath: binlogPath,
                ErrorSummary: summary,
                PropertyIssuesJson: JsonSerializer.Serialize(propertyIssues),
                EnvReadsJson: JsonSerializer.Serialize(envReads),
                CallStackJson: null,
                DurationMs: null);
        }
        catch
        {
            return new BuildFailureIngestRequest(
                Id: null,
                Timestamp: TimeProvider.System.GetUtcNow(),
                Target: target,
                ExitCode: exitCode,
                BinlogPath: binlogPath,
                ErrorSummary: "Failed to parse binlog",
                PropertyIssuesJson: null,
                EnvReadsJson: null,
                CallStackJson: null,
                DurationMs: null);
        }
    }
}
