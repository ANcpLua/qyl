using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;

namespace qyl.collector.Ingestion;

public sealed partial class PdbSourceResolver
{
    public SourceLocation? ResolveFromStackTrace(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return null;

        foreach (var line in stackTrace.Split('\n',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = StackFrameRegex().Match(line);
            if (!match.Success)
                continue;

            var file = match.Groups["file"].Value;
            var lineNumber = int.TryParse(match.Groups["line"].Value, out var ln) ? ln : (int?)null;
            return new SourceLocation(file, lineNumber, null, null);
        }

        return null;
    }

    public SourceLocation? ResolveFromCurrentMethod(string? methodName)
    {
        if (string.IsNullOrWhiteSpace(methodName))
            return null;

        // Best-effort fallback: use current stack symbol info if available.
        var stackTrace = new StackTrace(true);
        foreach (var frame in stackTrace.GetFrames() ?? [])
        {
            var method = frame.GetMethod();
            if (method is null)
                continue;

            var fullName = method.DeclaringType is null
                ? method.Name
                : $"{method.DeclaringType.FullName}.{method.Name}";

            if (!string.Equals(fullName, methodName, StringComparison.Ordinal) &&
                !string.Equals(method.Name, methodName, StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = frame.GetFileName();
            var line = frame.GetFileLineNumber();
            var column = frame.GetFileColumnNumber();
            if (string.IsNullOrWhiteSpace(fileName) && line == 0 && column == 0)
                return null;

            return new SourceLocation(fileName, line > 0 ? line : null, column > 0 ? column : null, fullName);
        }

        return null;
    }

    public bool HasPortablePdb(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
            return false;

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);

        var entry = peReader.ReadDebugDirectory()
            .FirstOrDefault(static d => d.Type == DebugDirectoryEntryType.CodeView);

        if (entry.DataSize == 0)
            return false;

        var data = peReader.ReadCodeViewDebugDirectoryData(entry);
        return File.Exists(Path.Combine(Path.GetDirectoryName(assemblyPath) ?? string.Empty, data.Path));
    }

    [GeneratedRegex(@"\sin\s(?<file>.*):line\s(?<line>\d+)", RegexOptions.Compiled)]
    private static partial Regex StackFrameRegex();
}
