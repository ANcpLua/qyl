using System.Text;
using Qyl.Collector.Storage;

namespace Qyl.Collector.Analysis;

internal sealed class AnalysisSigningKey
{
    private readonly byte[] _key;

    public AnalysisSigningKey(IConfiguration configuration)
    {
        var configured = configuration["QYL_ANALYSIS_SIGNING_KEY"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (Encoding.UTF8.GetByteCount(configured) < 32)
                throw new InvalidOperationException("QYL_ANALYSIS_SIGNING_KEY must contain at least 32 bytes.");
            _key = Encoding.UTF8.GetBytes(configured);
            return;
        }
        // Local collectors persist an independent random key beside their database.
        // Rotating an ingest/read credential never invalidates signed continuations.
        var database = configuration["QYL_DATA_PATH"] ?? StorageDefaults.DatabaseFileName;
        if (database == ":memory:") { _key = RandomNumberGenerator.GetBytes(32); return; }
        var path = Path.GetFullPath(database) + ".analysis-signing.key";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using var file = new FileStream(path, options);
            file.Write(RandomNumberGenerator.GetBytes(32));
        }
        catch (IOException) when (File.Exists(path)) { _key = File.ReadAllBytes(path); }
        _key = File.ReadAllBytes(path);
        if (_key.Length < 32) throw new InvalidOperationException("The stored analysis signing key is invalid.");
    }

    public AnalysisHandles ForProject(string project) => new(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes("qyl-analysis-v1\0" + project)));
}
